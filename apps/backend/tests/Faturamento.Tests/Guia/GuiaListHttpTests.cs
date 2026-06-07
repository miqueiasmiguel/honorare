using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Catalog;
using Faturamento.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faturamento.Tests.ListEndpoints;

// Exercita o binding real query-string → enum/bool no endpoint GET /api/v1/admin/guias.
// Os testes de serviço constroem ListarGuiasQuery diretamente e não cobrem essa camada;
// se o binding de enum falhasse, a ordenação viraria no-op silencioso.
[Collection(nameof(PostgresCollection))]
public sealed class GuiaListHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public GuiaListHttpTests(PostgresContainerFixture db)
    {
        _db = db;
        _factory = BuildFactory(db);
    }

#pragma warning disable CA2000
    private static WebApplicationFactory<Program> BuildFactory(PostgresContainerFixture db) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = db.ConnectionString,
                    ["Google:ClientId"] = "test",
                    ["Google:ClientSecret"] = "test",
                    ["Jwt:Secret"] = "test-secret-key-for-jwt-at-least-32-chars!!",
                    ["Jwt:Issuer"] = "test",
                    ["Jwt:Audience"] = "test",
                }));
            b.ConfigureTestServices(services =>
            {
                services.Configure<AuthenticationOptions>(opt =>
                {
                    opt.DefaultAuthenticateScheme = TestListAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestListAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestListAuthHandler>(
                        TestListAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth-List", "true");
        return client;
    }

    private async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)> SeedCatalogAsync()
    {
        await using var ctx = _db.CreateContext();
        var tenantId = TestListIdentity.TenantId;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var prestador = Prestador.Create(tenantId, "Dr. ListHttp " + suffix, null);
        var operadora = Operadora.Create(tenantId, "UNIMED ListHttp " + suffix, null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, suffix, "Proc ListHttp " + suffix, "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        ctx.Add(DeflatorPrestador.Create(tenantId, prestador.Id, operadora.Id, PosicaoExecutor.Cirurgiao, 100m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    private static async Task CriarGuiaAsync(
        HttpClient client, Guid prestadorId, Guid operadoraId, Guid procedimentoId,
        string numeroGuia, string dataAtendimento)
    {
        var body = new
        {
            prestadorId,
            operadoraId,
            beneficiarioId = (Guid?)null,
            numeroGuia,
            dataAtendimento,
            ehPacote = false,
            observacao = "",
            itens = new[]
            {
                new
                {
                    procedimentoId,
                    posicaoExecutor = "Cirurgiao",
                    percentualOrdem = 1.0m,
                    viaAcesso = "Convencional",
                    acomodacao = "Enfermaria",
                    ehUrgencia = false,
                    valorApurado = (decimal?)null,
                },
            },
        };
        var response = await client.PostAsJsonAsync("/api/v1/admin/guias", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<List<string>> ListarNumerosGuiaAsync(HttpClient client, string queryString)
    {
        var response = await client.GetAsync(new Uri(queryString, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("itens").EnumerateArray()
            .Select(e => e.GetProperty("numeroGuia").GetString()!)
            .Where(n => n.StartsWith("LH-", StringComparison.Ordinal))
            .ToList();
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant List HTTP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestListIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_Guias_OrdenarPorNumeroGuia_BindaEnumEBoolDaQueryStringAsync()
    {
        var (prestadorId, operadoraId, procedimentoId) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        // mesma data para isolar a ordenação por NumeroGuia
        await CriarGuiaAsync(client, prestadorId, operadoraId, procedimentoId, "LH-ZZZ", "2025-06-01");
        await CriarGuiaAsync(client, prestadorId, operadoraId, procedimentoId, "LH-AAA", "2025-06-01");
        await CriarGuiaAsync(client, prestadorId, operadoraId, procedimentoId, "LH-MMM", "2025-06-01");

        var asc = await ListarNumerosGuiaAsync(
            client, "/api/v1/admin/guias?ordenarPor=NumeroGuia&descendente=false&itensPorPagina=100");
        var desc = await ListarNumerosGuiaAsync(
            client, "/api/v1/admin/guias?ordenarPor=NumeroGuia&descendente=true&itensPorPagina=100");

        Assert.Equal(["LH-AAA", "LH-MMM", "LH-ZZZ"], asc);
        Assert.Equal(["LH-ZZZ", "LH-MMM", "LH-AAA"], desc);
    }
}

file static class TestListIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestListAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestList";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth-List"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestListIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestListIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
