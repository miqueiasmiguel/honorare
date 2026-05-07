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

namespace Catalog.Tests.Beneficiario;

[Collection(nameof(PostgresCollection))]
public sealed class BeneficiarioEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public BeneficiarioEndpointTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestBeneficiarioAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestBeneficiarioAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestBeneficiarioAuthHandler>(
                        TestBeneficiarioAuthHandler.SchemeName, _ => { });
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
              VALUES ({0}, 'Test Tenant Beneficiario EP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestBeneficiarioIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_Beneficiarios_SemAutenticacao_Retorna401Async()
    {
        using var client = CreateUnauthenticatedClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/admin/beneficiarios", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Beneficiarios_Autenticado_Retorna200ComListaAsync()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/admin/beneficiarios", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_LookupOrCreate_CarteiraNova_Retorna201ComCriadoTrueELocationHeaderAsync()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { carteira = "EP-NOVA-001", nome = "Paciente Novo" };

        var response = await client.PostAsJsonAsync("/api/v1/admin/beneficiarios/lookup-or-create", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("criado").GetBoolean());
        Assert.Equal("EP-NOVA-001", doc.RootElement.GetProperty("carteira").GetString());
    }

    [Fact]
    public async Task POST_LookupOrCreate_CarteiraNova_LocationHeaderAponta_ParaRecursoAsync()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { carteira = "EP-LOCATION-002", nome = "Paciente Location" };

        var response = await client.PostAsJsonAsync("/api/v1/admin/beneficiarios/lookup-or-create", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var id = doc.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(
            $"/api/v1/admin/beneficiarios/{id}",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task POST_LookupOrCreate_CarteiraExistente_Retorna200ComCriadoFalseAsync()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { carteira = "EP-EXISTE-003", nome = "Paciente Existente" };

        await client.PostAsJsonAsync("/api/v1/admin/beneficiarios/lookup-or-create", body);
        var response = await client.PostAsJsonAsync("/api/v1/admin/beneficiarios/lookup-or-create", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("criado").GetBoolean());
    }

    [Fact]
    public async Task POST_LookupOrCreate_BodyVazio_Retorna400Async()
    {
        using var client = CreateAuthenticatedClient();
        var body = new { carteira = "", nome = "" };

        var response = await client.PostAsJsonAsync("/api/v1/admin/beneficiarios/lookup-or-create", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_Beneficiarios_PorId_Existente_Retorna200Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/beneficiarios/lookup-or-create",
            new { carteira = "EP-GET-004", nome = "Paciente Get" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/beneficiarios/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_Beneficiarios_PorId_Inexistente_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/beneficiarios/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Beneficiarios_NomeValido_Retorna200Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/beneficiarios/lookup-or-create",
            new { carteira = "EP-PUT-005", nome = "Nome Antigo" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/beneficiarios/{id}",
            new { nome = "Nome Atualizado" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Nome Atualizado", doc.RootElement.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task PUT_Beneficiarios_NomeVazio_Retorna400Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/beneficiarios/lookup-or-create",
            new { carteira = "EP-PUT-VAZ-006", nome = "Nome Válido" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/beneficiarios/{id}",
            new { nome = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Beneficiarios_IdInexistente_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/beneficiarios/{Guid.NewGuid()}",
            new { nome = "Qualquer Nome" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Beneficiarios_Existente_Retorna204Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/beneficiarios/lookup-or-create",
            new { carteira = "EP-DEL-007", nome = "Paciente Delete" });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/beneficiarios/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Beneficiarios_Inexistente_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/beneficiarios/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

file static class TestBeneficiarioIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestBeneficiarioAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestBeneficiario";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestBeneficiarioIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestBeneficiarioIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
