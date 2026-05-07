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

namespace Faturamento.Tests.Endpoints;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public GuiaEndpointTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestGuiaAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestGuiaAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestGuiaAuthHandler>(
                        TestGuiaAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth", "true");
        return client;
    }

    private HttpClient CreateUnauthenticatedClient() =>
        _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId)>
        SeedCatalogAsync()
    {
        await using var ctx = _db.CreateContext();

        var tenantId = TestGuiaIdentity.TenantId;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var prestador = Prestador.Create(tenantId, "Dr. Endpoint " + suffix, null);
        var operadora = Operadora.Create(tenantId, "UNIMED EP " + suffix, null, null, TipoRuleSet.Unimed);
        var beneficiario = Beneficiario.Create(tenantId, "EP" + suffix, "Paciente EP " + suffix);
        var procedimento = Procedimento.Create(tenantId, "EP" + suffix, "Procedimento EP " + suffix, "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(beneficiario);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, beneficiario.Id, procedimento.Id);
    }

    private static object ItemBody(Guid procedimentoId) => new
    {
        procedimentoId,
        posicaoExecutor = "Cirurgiao",
        ordemProcedimento = "Unico",
        viaAcesso = "Convencional",
        acomodacao = "Enfermaria",
        ehUrgencia = false,
        valorApurado = (decimal?)null,
    };

    private static object GuiaBody(Guid prestadorId, Guid operadoraId, Guid beneficiarioId, Guid procedimentoId) => new
    {
        prestadorId,
        operadoraId,
        beneficiarioId,
        senha = "SEN-EP-001",
        dataAtendimento = "2025-06-01",
        ehPacote = false,
        observacao = "Obs endpoint",
        itens = new[] { ItemBody(procedimentoId) },
    };

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant Guia EP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestGuiaIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_Guias_SemAutenticacao_Retorna401Async()
    {
        using var client = CreateUnauthenticatedClient();

        var response = await client.GetAsync(new Uri("/api/v1/admin/guias", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Guias_Autenticado_Retorna200ComPaginacaoAsync()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/admin/guias?pagina=1&itensPorPagina=10", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("itens", out _));
        Assert.True(doc.RootElement.TryGetProperty("total", out _));
        Assert.True(doc.RootElement.TryGetProperty("pagina", out _));
        Assert.True(doc.RootElement.TryGetProperty("itensPorPagina", out _));
    }

    [Fact]
    public async Task GET_GuiaPorId_Existente_Retorna200ComItensAsync()
    {
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/guias",
            GuiaBody(prestadorId, operadoraId, beneficiarioId, procedimentoId));
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/guias/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("itens", out _));
    }

    [Fact]
    public async Task GET_GuiaPorId_Inexistente_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/guias/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_Guias_DadosValidos_Retorna201ComLocationAsync()
    {
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/guias",
            GuiaBody(prestadorId, operadoraId, beneficiarioId, procedimentoId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("SEN-EP-001", doc.RootElement.GetProperty("senha").GetString());
    }

    [Fact]
    public async Task POST_Guias_SemItens_Retorna400Async()
    {
        var (prestadorId, operadoraId, beneficiarioId, _) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var body = new
        {
            prestadorId,
            operadoraId,
            beneficiarioId,
            senha = "SEN-EP-SEM",
            dataAtendimento = "2025-06-01",
            ehPacote = false,
            observacao = "",
            itens = Array.Empty<object>(),
        };

        var response = await client.PostAsJsonAsync("/api/v1/admin/guias", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Guias_DadosValidos_Retorna200AtualizadoAsync()
    {
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/guias",
            GuiaBody(prestadorId, operadoraId, beneficiarioId, procedimentoId));
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var updateBody = new
        {
            operadoraId,
            beneficiarioId,
            senha = "SEN-EP-UPD",
            dataAtendimento = "2025-07-01",
            ehPacote = false,
            observacao = "Atualizado",
            itens = new[] { ItemBody(procedimentoId) },
        };

        var response = await client.PutAsJsonAsync($"/api/v1/admin/guias/{id}", updateBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("SEN-EP-UPD", doc.RootElement.GetProperty("senha").GetString());
    }

    [Fact]
    public async Task DELETE_Guias_Existente_Retorna204Async()
    {
        var (prestadorId, operadoraId, beneficiarioId, procedimentoId) = await SeedCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/guias",
            GuiaBody(prestadorId, operadoraId, beneficiarioId, procedimentoId));
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/guias/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Guias_Inexistente_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/guias/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

file static class TestGuiaIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestGuiaAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestGuia";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestGuiaIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestGuiaIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
