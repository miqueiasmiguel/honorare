using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Catalog;
using App.Faturamento;
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

namespace Faturamento.Tests.Medico;

[Collection(nameof(PostgresCollection))]
public sealed class MedicoGuiaTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public MedicoGuiaTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestMedicoAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestMedicoAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestMedicoAuthHandler>(
                        TestMedicoAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateMedicoClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Medico-Auth", "true");
        return client;
    }

    private static async Task<Guid> SeedGuiaAsync(
        App.Data.AppDbContext ctx, Guid tenantId, Guid prestadorId, Guid operadoraId,
        Guid? beneficiarioId, Guid procedimentoId, string numeroGuia, DateOnly data)
    {
        var guia = Guia.Create(tenantId, prestadorId, operadoraId, beneficiarioId,
            numeroGuia, data, false, string.Empty);
        ctx.Guias.Add(guia);
        var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.ItensGuia.Add(item);
        await ctx.SaveChangesAsync();
        return guia.Id;
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant Medico', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestMedicoIdentity.TenantId);

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO prestadores (""Id"", ""TenantId"", ""Nome"", ""RegistroProfissional"", ""Ativo"", ""CriadoEm"")
              VALUES ({0}, {1}, 'Dr. Medico Test', NULL, true, NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestMedicoIdentity.MedicoId, TestMedicoIdentity.TenantId);

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO operadoras (""Id"", ""TenantId"", ""Nome"", ""RegistroAns"", ""Cnpj"", ""TipoRuleSet"", ""Ativa"", ""CriadaEm"")
              VALUES ({0}, {1}, 'UNIMED-MEDTEST', NULL, NULL, 'Unimed', true, NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestMedicoIdentity.OperadoraId, TestMedicoIdentity.TenantId);

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO beneficiarios (""Id"", ""TenantId"", ""Carteira"", ""Nome"", ""CriadoEm"")
              VALUES ({0}, {1}, 'MGTST001', 'Paciente Test', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestMedicoIdentity.BeneficiarioId, TestMedicoIdentity.TenantId);

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO procedimentos (""Id"", ""TenantId"", ""CodigoTuss"", ""Descricao"", ""Porte"", ""PorteAnestesico"", ""EhSadt"", ""TemPorteProprioVideo"", ""Ativo"", ""CriadoEm"")
              VALUES ({0}, {1}, 'MGTST001', 'Proc Medico Test', NULL, NULL, false, false, true, NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestMedicoIdentity.ProcedimentoId, TestMedicoIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task MedicoNaoVeGuiasDeMedicoDiferenteAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var outroPrestador = Prestador.Create(tenantId, "Dr. Outro MGM", null);
        ctx.Add(outroPrestador);
        await ctx.SaveChangesAsync();

        await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "MGM-001", new DateOnly(2025, 1, 10));
        await SeedGuiaAsync(ctx, tenantId, outroPrestador.Id, operadoraId, beneficiarioId, procedimentoId, "MGM-002", new DateOnly(2025, 1, 11));

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(new Uri("/api/v1/medico/guias?itensPorPagina=100", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();
        Assert.All(itens, item => Assert.NotEqual("MGM-002", item.GetProperty("numeroGuia").GetString()));
        Assert.Contains(itens, item => item.GetProperty("numeroGuia").GetString() == "MGM-001");
    }

    [Fact]
    public async Task MedicoNaoVeGuiasLiquidadasAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var guiaId = await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "LIQ-MEDICO-01", new DateOnly(2025, 2, 1));

        var guia = await ctx.Guias.FindAsync(guiaId);
        guia!.Liquidar();
        await ctx.SaveChangesAsync();

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(new Uri("/api/v1/medico/guias?itensPorPagina=100", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();
        Assert.All(itens, item => Assert.NotEqual("LIQ-MEDICO-01", item.GetProperty("numeroGuia").GetString()));
    }

    [Fact]
    public async Task MedicoVeGuiasApresentadaEEmRecursoAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "APRES-MED-01", new DateOnly(2025, 3, 1));
        var emRecId = await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "EMREC-MED-01", new DateOnly(2025, 3, 2));

        var recurso = Recurso.Create(tenantId, operadoraId, medicoId, new DateOnly(2025, 3, 1), null, "202502");
        ctx.Recursos.Add(recurso);
        await ctx.SaveChangesAsync();

        var guiaEmRec = await ctx.Guias.FindAsync(emRecId);
        guiaEmRec!.MarcarEmRecurso(recurso.Id);
        await ctx.SaveChangesAsync();

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(new Uri("/api/v1/medico/guias?itensPorPagina=100", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var numeros = doc.RootElement.GetProperty("itens").EnumerateArray()
            .Select(i => i.GetProperty("numeroGuia").GetString())
            .ToList();

        Assert.Contains("APRES-MED-01", numeros);
        Assert.Contains("EMREC-MED-01", numeros);
    }

    [Fact]
    public async Task FiltroOperadoraFuncionaAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var outraOp = Operadora.Create(tenantId, "AMIL FO " + uid, null, null, TipoRuleSet.Nulo);
        ctx.Add(outraOp);
        await ctx.SaveChangesAsync();

        await SeedGuiaAsync(ctx, tenantId, medicoId, TestMedicoIdentity.OperadoraId, beneficiarioId, procedimentoId, "FO-UNIMED-01", new DateOnly(2025, 4, 1));
        await SeedGuiaAsync(ctx, tenantId, medicoId, outraOp.Id, beneficiarioId, procedimentoId, "FO-AMIL-01", new DateOnly(2025, 4, 2));

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/medico/guias?operadoraId={outraOp.Id}&itensPorPagina=100", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();

        Assert.All(itens, item => Assert.NotEqual("FO-UNIMED-01", item.GetProperty("numeroGuia").GetString()));
        Assert.Contains(itens, item => item.GetProperty("numeroGuia").GetString() == "FO-AMIL-01");
    }

    [Fact]
    public async Task FiltroDataInicioFimAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "FD-JAN-01", new DateOnly(2025, 1, 5));
        await SeedGuiaAsync(ctx, tenantId, medicoId, operadoraId, beneficiarioId, procedimentoId, "FD-JUN-01", new DateOnly(2025, 6, 15));

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(
            new Uri("/api/v1/medico/guias?dataInicio=2025-06-01&dataFim=2025-06-30&itensPorPagina=100", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();

        Assert.All(itens, item => Assert.NotEqual("FD-JAN-01", item.GetProperty("numeroGuia").GetString()));
        Assert.Contains(itens, item => item.GetProperty("numeroGuia").GetString() == "FD-JUN-01");
    }

    [Fact]
    public async Task DetalheRetorna404SeGuiaNaoEDoMedicoAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var outroPrestador = Prestador.Create(tenantId, "Dr. 404 M " + uid, null);
        ctx.Add(outroPrestador);
        await ctx.SaveChangesAsync();

        var guiaId = await SeedGuiaAsync(ctx, tenantId, outroPrestador.Id, operadoraId, beneficiarioId, procedimentoId, "D404-01", new DateOnly(2025, 5, 1));

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/medico/guias/{guiaId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DetalheEmbuteSituacaoCalculoPorItemAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var guia = Guia.Create(tenantId, medicoId, operadoraId, beneficiarioId,
                        "DET-CALC-01", new DateOnly(2025, 7, 1), false, string.Empty);
        ctx.Guias.Add(guia);
        var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, 100m);
        ctx.ItensGuia.Add(item);
        var calculo = Calculo.Create(tenantId, guia.Id);
        ctx.Calculos.Add(calculo);
        await ctx.SaveChangesAsync();

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/medico/guias/{guia.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();
        Assert.Single(itens);
        Assert.Equal("Calculado", itens[0].GetProperty("situacaoCalculo").GetString());
    }

    [Fact]
    public async Task DetalheRetornaNaoCalculadoSemCalculoAsync()
    {
        var tenantId = TestMedicoIdentity.TenantId;
        var medicoId = TestMedicoIdentity.MedicoId;
        var operadoraId = TestMedicoIdentity.OperadoraId;
        var beneficiarioId = TestMedicoIdentity.BeneficiarioId;
        var procedimentoId = TestMedicoIdentity.ProcedimentoId;

        await using var ctx = _db.CreateTenantContext(tenantId);
        var guia = Guia.Create(tenantId, medicoId, operadoraId, beneficiarioId,
                        "DET-NCALC-01", new DateOnly(2025, 8, 1), false, string.Empty);
        ctx.Guias.Add(guia);
        var item = ItemGuia.Create(guia.Id, procedimentoId, PosicaoExecutor.Cirurgiao,
            1.0m, ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
        ctx.ItensGuia.Add(item);
        await ctx.SaveChangesAsync();

        using var client = CreateMedicoClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/medico/guias/{guia.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var itens = doc.RootElement.GetProperty("itens").EnumerateArray().ToList();
        Assert.Single(itens);
        Assert.Equal("NaoCalculado", itens[0].GetProperty("situacaoCalculo").GetString());
    }
}

file static class TestMedicoIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
    internal static readonly Guid MedicoId = Guid.NewGuid();
    internal static readonly Guid OperadoraId = Guid.NewGuid();
    internal static readonly Guid BeneficiarioId = Guid.NewGuid();
    internal static readonly Guid ProcedimentoId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestMedicoAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestMedico";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Medico-Auth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestMedicoIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "Medico"),
            new Claim("role", "Medico"),
            new Claim("tenant_id", TestMedicoIdentity.TenantId.ToString()),
            new Claim("medico_id", TestMedicoIdentity.MedicoId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
