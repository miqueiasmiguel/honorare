using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Catalog;
using App.Data;
using App.Faturamento;
using App.Faturamento.Motor;
using App.Identity;
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

namespace Faturamento.Tests.CalculoEndpoints;

[Collection(nameof(PostgresCollection))]
public sealed class GuiaCalculoEndpointTests(PostgresContainerFixture db)
{
    private (AppDbContext ctx, ICurrentUser user) BuildTenant(Guid tenantId)
    {
        var currentUser = new FakeVisUser(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.ConnectionString)
            .Options;
        return (new AppDbContext(options, currentUser), currentUser);
    }

    private static async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)>
        SeedBaseAsync(AppDbContext ctx, Guid tenantId)
    {
        var suffix = tenantId.ToString("N")[..8];
        var prestador = Prestador.Create(tenantId, "Dr. Vis " + suffix, null);
        var operadora = Operadora.Create(tenantId, "Op Vis " + suffix, null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, suffix, "Proc Vis " + suffix, "1", null, false, false);
        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();
        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    [Fact]
    public async Task ObterCalculo_GuiaComCalculoCompleto_RetornaItemCalculadoComPassosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        ctx.Add(TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, 200m));
        await ctx.SaveChangesAsync();

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-VIS01",
            new DateOnly(2025, 1, 1), false, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null)]);

        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var result = await service.ObterCalculoAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.EhPacote);
        Assert.NotNull(result.Value.RealizadoEm);
        Assert.Single(result.Value.Itens);
        var item = result.Value.Itens[0];
        Assert.Equal("Calculado", item.Situacao);
        Assert.NotNull(item.ValorApurado);
        Assert.Contains(item.Passos, p => p.Regra == "ValorBase");
    }

    [Fact]
    public async Task ObterCalculo_GuiaLegadaSemTabela_RetornaItemSemTabelaSemPassosAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        // Create guia directly (bypasses service validation) to simulate a legacy uncalculated guia
        var guia = Guia.Create(tenantId, prestadorId, operadoraId, null,
            "SEN-VIS02", new DateOnly(2025, 1, 1), false, string.Empty);
        ctx.Add(guia);
        ctx.Add(ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
        ctx.Add(Calculo.Create(tenantId, guia.Id));
        await ctx.SaveChangesAsync();

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);
        var result = await service.ObterCalculoAsync(guia.Id);

        Assert.True(result.IsSuccess);
        var item = result.Value!.Itens[0];
        Assert.Null(item.ValorApurado);
        Assert.Empty(item.Passos);
        Assert.Equal("SemTabela", item.Situacao);
    }

    [Fact]
    public async Task ObterCalculo_GuiaPacote_RetornaEhPacoteTrueComSituacaoPacoteAsync()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, user) = BuildTenant(tenantId);
        await using var _ = ctx;
        var (prestadorId, operadoraId, procedimentoId) = await SeedBaseAsync(ctx, tenantId);

        var factory = new PricingRuleSetFactory(ctx);
        var service = new GuiaService(ctx, user, factory);

        var cmd = new CriarGuiaCommand(prestadorId, operadoraId, null, "SEN-VIS03",
            new DateOnly(2025, 1, 1), true, string.Empty,
            [new CriarItemGuiaCommand(procedimentoId, PosicaoExecutor.Cirurgiao,
                ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 350m)]);

        var criado = await service.CriarAsync(cmd);
        Assert.True(criado.IsSuccess);

        var result = await service.ObterCalculoAsync(criado.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.EhPacote);
        Assert.Null(result.Value.RealizadoEm);
        var item = result.Value.Itens[0];
        Assert.Equal("Pacote", item.Situacao);
        Assert.Equal(350m, item.ValorApurado);
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class GuiaCalculoHttpTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public GuiaCalculoHttpTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestCalcAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestCalcAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestCalcAuthHandler>(
                        TestCalcAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth-Calc", "true");
        return client;
    }

    private async Task<(Guid prestadorId, Guid operadoraId, Guid procedimentoId)> SeedCalcCatalogAsync()
    {
        await using var ctx = _db.CreateContext();
        var tenantId = TestCalcIdentity.TenantId;
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var prestador = Prestador.Create(tenantId, "Dr. CalcHttp " + suffix, null);
        var operadora = Operadora.Create(tenantId, "UNIMED CalcHttp " + suffix, null, null, TipoRuleSet.Unimed);
        var procedimento = Procedimento.Create(tenantId, suffix, "Proc CalcHttp " + suffix, "1", null, false, false);

        ctx.Add(prestador);
        ctx.Add(operadora);
        ctx.Add(procedimento);
        await ctx.SaveChangesAsync();

        ctx.Add(TabelaProcedimento.Create(tenantId, operadora.Id, procedimento.Id, 200m));
        await ctx.SaveChangesAsync();

        return (prestador.Id, operadora.Id, procedimento.Id);
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant Calc HTTP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestCalcIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_GuiaCalculo_Calculada_Retorna200ComPassosAsync()
    {
        var (prestadorId, operadoraId, procedimentoId) = await SeedCalcCatalogAsync();
        using var client = CreateAuthenticatedClient();

        var createBody = new
        {
            prestadorId,
            operadoraId,
            beneficiarioId = (Guid?)null,
            numeroGuia = "SEN-HTTP-CAL",
            dataAtendimento = "2025-06-01",
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

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/guias", createBody);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/guias/{id}/calculo", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens");
        Assert.True(itens.GetArrayLength() > 0);
        var passos = itens[0].GetProperty("passos");
        Assert.True(passos.GetArrayLength() > 0);
    }
}

file static class TestCalcIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

file sealed class FakeVisUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
    public bool IsAuthenticated => true;
}

#pragma warning disable CA1812
file sealed class TestCalcAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestCalc";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth-Calc"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestCalcIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestCalcIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
