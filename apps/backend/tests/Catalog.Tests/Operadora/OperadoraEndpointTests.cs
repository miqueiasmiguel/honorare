using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

namespace Catalog.Tests.Operadora;

[Collection(nameof(PostgresCollection))]
public sealed class OperadoraEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public OperadoraEndpointTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestOperadoraAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestOperadoraAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestOperadoraAuthHandler>(
                        TestOperadoraAuthHandler.SchemeName, _ => { });
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

    public async Task InitializeAsync()
    {
        await using var ctx = _db.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Tenants"" (""Id"", ""Name"", ""Status"", ""CreatedAt"")
              VALUES ({0}, 'Test Tenant EP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestOperadoraIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_Operadoras_SemAutenticacao_Retorna401Async()
    {
        using var client = CreateUnauthenticatedClient();

        var response = await client.GetAsync(new Uri("/api/v1/admin/operadoras", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Operadoras_ComTenantAdmin_Retorna200ComListaAsync()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(new Uri("/api/v1/admin/operadoras", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_Operadoras_DadosValidos_Retorna201ComOperadoraCriadaAsync()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { nome = "UNIMED JP", tipoRuleSet = "Unimed" };

        var response = await client.PostAsJsonAsync("/api/v1/admin/operadoras", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("UNIMED JP", doc.RootElement.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task POST_Operadoras_NomeFaltando_Retorna400Async()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { nome = "", tipoRuleSet = "Unimed" };

        var response = await client.PostAsJsonAsync("/api/v1/admin/operadoras", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_Operadoras_CnpjDuplicado_Retorna409Async()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { nome = "UNIMED 409", cnpj = "44444444000100", tipoRuleSet = "Unimed" };

        await client.PostAsJsonAsync("/api/v1/admin/operadoras", body);
        var response = await client.PostAsJsonAsync("/api/v1/admin/operadoras", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GET_OperadoraPorId_Existente_Retorna200Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/operadoras",
            new { nome = "UNIMED GET", tipoRuleSet = "Unimed" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/operadoras/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_OperadoraPorId_NaoEncontrada_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/operadoras/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Operadora_DadosValidos_Retorna200AtualizadoAsync()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/operadoras",
            new { nome = "UNIMED PUT", tipoRuleSet = "Unimed" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var updateBody = new { nome = "UNIMED PUT Atualizado", tipoRuleSet = "Unimed", ativa = true };
        var response = await client.PutAsJsonAsync($"/api/v1/admin/operadoras/{id}", updateBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("UNIMED PUT Atualizado", doc.RootElement.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task DELETE_Operadora_Existente_Retorna204Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/operadoras",
            new { nome = "UNIMED DELETE", tipoRuleSet = "Unimed" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/operadoras/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Operadora_NaoEncontrada_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/operadoras/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

file static class TestOperadoraIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestOperadoraAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestOperadora";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestOperadoraIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestOperadoraIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
