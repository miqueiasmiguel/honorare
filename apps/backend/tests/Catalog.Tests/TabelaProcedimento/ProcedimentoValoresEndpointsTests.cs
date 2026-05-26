using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Catalog;
using Catalog.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catalog.Tests.TabelaProcedimento;

[Collection(nameof(PostgresCollection))]
public sealed class ProcedimentoValoresEndpointsTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public ProcedimentoValoresEndpointsTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestProcValoresAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestProcValoresAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestProcValoresAuthHandler>(
                        TestProcValoresAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth-ProcValores", "true");
        return client;
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant ProcValores EP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestProcValoresIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<Guid> SeedProcedimentoAsync(Guid tenantId, string codigoTuss)
    {
        await using var ctx = _db.CreateContext();
        var proc = App.Catalog.Procedimento.Create(
            tenantId, codigoTuss, "Proc " + codigoTuss, null, null, false, false);
        ctx.Procedimentos.Add(proc);
        await ctx.SaveChangesAsync();
        return proc.Id;
    }

    private async Task<Guid> SeedOperadoraAsync(Guid tenantId, string nome, bool ativa = true)
    {
        await using var ctx = _db.CreateContext();
        var op = App.Catalog.Operadora.Create(tenantId, nome, null, null, TipoRuleSet.Unimed);
        if (!ativa)
        {
            op.Atualizar(nome, null, null, TipoRuleSet.Unimed, false);
        }

        ctx.Operadoras.Add(op);
        await ctx.SaveChangesAsync();
        return op.Id;
    }

    private async Task<Guid> SeedTabelaAsync(Guid tenantId, Guid operadoraId, Guid procedimentoId, decimal valor)
    {
        await using var ctx = _db.CreateContext();
        var tabela = App.Catalog.TabelaProcedimento.Create(tenantId, operadoraId, procedimentoId, valor);
        ctx.TabelasProcedimento.Add(tabela);
        await ctx.SaveChangesAsync();
        return tabela.Id;
    }

    private async Task<bool> TabelaExisteAsync(Guid id)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.TabelasProcedimento.AnyAsync(t => t.Id == id);
    }

    // ── Listar ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_ProcedimentoSemValores_RetornaUmaLinhaPorOperadoraAtivaAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var op1 = await SeedOperadoraAsync(tenantId, NextNome("OPA"));
        var op2 = await SeedOperadoraAsync(tenantId, NextNome("OPB"));
        var op3 = await SeedOperadoraAsync(tenantId, NextNome("OPC"));

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/procedimentos/{procId}/valores", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var itens = await ReadValoresAsync(response);
        var encontradas = itens.Where(i => i.OperadoraId == op1 || i.OperadoraId == op2 || i.OperadoraId == op3).ToList();
        Assert.Equal(3, encontradas.Count);
        Assert.All(encontradas, i =>
        {
            Assert.Null(i.TabelaId);
            Assert.Null(i.Valor);
        });
    }

    [Fact]
    public async Task Listar_ProcedimentoComValorParcial_RetornaTodasOperadorasEMarcaTabelaIdAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var op1 = await SeedOperadoraAsync(tenantId, NextNome("OP1"));
        var op2 = await SeedOperadoraAsync(tenantId, NextNome("OP2"));
        var op3 = await SeedOperadoraAsync(tenantId, NextNome("OP3"));
        await SeedTabelaAsync(tenantId, op1, procId, 526.50m);

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/procedimentos/{procId}/valores", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var itens = await ReadValoresAsync(response);

        var linhaOp1 = itens.Single(i => i.OperadoraId == op1);
        Assert.NotNull(linhaOp1.TabelaId);
        Assert.Equal(526.50m, linhaOp1.Valor);

        var linhaOp2 = itens.Single(i => i.OperadoraId == op2);
        Assert.Null(linhaOp2.TabelaId);
        Assert.Null(linhaOp2.Valor);

        var linhaOp3 = itens.Single(i => i.OperadoraId == op3);
        Assert.Null(linhaOp3.TabelaId);
        Assert.Null(linhaOp3.Valor);
    }

    [Fact]
    public async Task Listar_OperadoraInativa_NaoApareceNaListaAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opAtiva = await SeedOperadoraAsync(tenantId, NextNome("ATIVA"));
        var opInativa = await SeedOperadoraAsync(tenantId, NextNome("INATIVA"), ativa: false);
        await SeedTabelaAsync(tenantId, opAtiva, procId, 100m);
        await SeedTabelaAsync(tenantId, opInativa, procId, 200m);

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/procedimentos/{procId}/valores", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var itens = await ReadValoresAsync(response);
        Assert.Contains(itens, i => i.OperadoraId == opAtiva);
        Assert.DoesNotContain(itens, i => i.OperadoraId == opInativa);
    }

    [Fact]
    public async Task Listar_TenantOutro_Retorna404Async()
    {
        var outroTenant = Guid.NewGuid();
        await using (var ctx = _db.CreateContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
                  VALUES ({0}, 'Tenant Outro Valores', 'Ativo', NOW())
                  ON CONFLICT (""Id"") DO NOTHING",
                outroTenant);
        }

        var procIdOutro = await SeedProcedimentoAsync(outroTenant, NextCodigoTuss());

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/procedimentos/{procIdOutro}/valores", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Upsert ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_NovoValor_CriaTabelaProcedimentoAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opId = await SeedOperadoraAsync(tenantId, NextNome("UPS"));

        using var client = CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/procedimentos/{procId}/valores/{opId}",
            new { valor = 410.00m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var ctx = _db.CreateContext();
        var tabela = await ctx.TabelasProcedimento
            .FirstOrDefaultAsync(t => t.OperadoraId == opId && t.ProcedimentoId == procId);
        Assert.NotNull(tabela);
        Assert.Equal(410.00m, tabela!.Valor);
    }

    [Fact]
    public async Task Upsert_ValorExistente_AtualizaSemDuplicarAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opId = await SeedOperadoraAsync(tenantId, NextNome("UPD"));
        await SeedTabelaAsync(tenantId, opId, procId, 526.50m);

        using var client = CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/procedimentos/{procId}/valores/{opId}",
            new { valor = 600.00m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var ctx = _db.CreateContext();
        var tabelas = await ctx.TabelasProcedimento
            .Where(t => t.OperadoraId == opId && t.ProcedimentoId == procId)
            .ToListAsync();
        Assert.Single(tabelas);
        Assert.Equal(600.00m, tabelas[0].Valor);
    }

    [Fact]
    public async Task Upsert_ValorNegativo_Retorna422Async()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opId = await SeedOperadoraAsync(tenantId, NextNome("NEG"));

        using var client = CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/procedimentos/{procId}/valores/{opId}",
            new { valor = -10m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── Excluir ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Excluir_TabelaInexistente_Retorna204IdempotenteAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opId = await SeedOperadoraAsync(tenantId, NextNome("DELNO"));

        using var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/procedimentos/{procId}/valores/{opId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Excluir_ValorExistente_RemoveAsync()
    {
        var tenantId = TestProcValoresIdentity.TenantId;
        var procId = await SeedProcedimentoAsync(tenantId, NextCodigoTuss());
        var opId = await SeedOperadoraAsync(tenantId, NextNome("DELOK"));
        var tabelaId = await SeedTabelaAsync(tenantId, opId, procId, 300m);

        using var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/procedimentos/{procId}/valores/{opId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await TabelaExisteAsync(tabelaId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NextCodigoTuss() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string NextNome(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..15];

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private static async Task<IReadOnlyList<ValorOperadoraResponse>> ReadValoresAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ValorOperadoraResponse>>(content, _jsonOpts)!;
    }

#pragma warning disable CA1812 // instanciada via JsonSerializer.Deserialize
    private sealed class ValorOperadoraResponse
    {
        public Guid OperadoraId { get; set; }
        public string OperadoraNome { get; set; } = string.Empty;
        public string TipoRuleSet { get; set; } = string.Empty;
        public Guid? TabelaId { get; set; }
        public decimal? Valor { get; set; }
        public DateTimeOffset? AtualizadoEm { get; set; }
    }
#pragma warning restore CA1812
}

file static class TestProcValoresIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestProcValoresAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestProcValores";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth-ProcValores"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestProcValoresIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestProcValoresIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
