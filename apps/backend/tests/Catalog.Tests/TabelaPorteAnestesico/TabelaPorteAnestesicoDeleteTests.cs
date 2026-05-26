using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

namespace Catalog.Tests.TabelaPorteAnestesico;

[Collection(nameof(PostgresCollection))]
public sealed class TabelaPorteAnestesicoDeleteTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public TabelaPorteAnestesicoDeleteTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestPorteDeleteAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestPorteDeleteAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestPorteDeleteAuthHandler>(
                        TestPorteDeleteAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth-PorteDelete", "true");
        return client;
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant Porte Delete', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestPorteDeleteIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<Guid> SeedOperadoraAsync(Guid tenantId, string nome)
    {
        await using var ctx = _db.CreateContext();
        var op = App.Catalog.Operadora.Create(tenantId, nome, null, null, TipoRuleSet.Unimed);
        ctx.Operadoras.Add(op);
        await ctx.SaveChangesAsync();
        return op.Id;
    }

    private async Task<Guid> SeedPorteAsync(Guid tenantId, Guid operadoraId, string letra)
    {
        await using var ctx = _db.CreateContext();
        var porte = App.Catalog.TabelaPorteAnestesico.Create(
            tenantId, operadoraId, letra, 150m, 240m);
        ctx.TabelasPorteAnestesico.Add(porte);
        await ctx.SaveChangesAsync();
        return porte.Id;
    }

    private async Task<bool> PorteExisteAsync(Guid id)
    {
        await using var ctx = _db.CreateContext();
        return await ctx.TabelasPorteAnestesico.AnyAsync(p => p.Id == id);
    }

    [Fact]
    public async Task Excluir_PorteAnestesico_RemoveAsync()
    {
        var tenantId = TestPorteDeleteIdentity.TenantId;
        var opId = await SeedOperadoraAsync(tenantId, NextNome("OP"));
        var porteId = await SeedPorteAsync(tenantId, opId, "A");

        using var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/tabelas-porte-anestesico/{porteId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await PorteExisteAsync(porteId));
    }

    private static string NextNome(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..15];
}

file static class TestPorteDeleteIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestPorteDeleteAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestPorteDelete";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth-PorteDelete"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestPorteDeleteIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestPorteDeleteIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
