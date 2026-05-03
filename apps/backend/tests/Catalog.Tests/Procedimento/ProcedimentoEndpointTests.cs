using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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

namespace Catalog.Tests.Procedimento;

[Collection(nameof(PostgresCollection))]
public sealed class ProcedimentoEndpointTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _db;
    private readonly WebApplicationFactory<Program> _factory;

    public ProcedimentoEndpointTests(PostgresContainerFixture db)
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
                    opt.DefaultAuthenticateScheme = TestProcedimentoAuthHandler.SchemeName;
                    opt.DefaultChallengeScheme = TestProcedimentoAuthHandler.SchemeName;
                });
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestProcedimentoAuthHandler>(
                        TestProcedimentoAuthHandler.SchemeName, _ => { });
            });
        });
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Auth-Proc", "true");
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
              VALUES ({0}, 'Test Tenant Proc EP', 'Ativo', NOW())
              ON CONFLICT (""Id"") DO NOTHING",
            TestProcedimentoIdentity.TenantId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GET_Procedimentos_SemAutenticacao_Retorna401Async()
    {
        using var client = CreateUnauthenticatedClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/admin/procedimentos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Procedimentos_ComTenantAdmin_Retorna200Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/admin/procedimentos", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_Procedimentos_DadosValidos_Retorna201Async()
    {
        using var client = CreateAuthenticatedClient();
        var body = new
        {
            codigoTuss = "30715013",
            descricao = "Herniorrafia inguinal",
            porte = "6B",
            porteAnestesico = 4,
            ehSadt = false,
            temPorteProprioVideo = false,
            ativo = true,
        };

        var response = await client.PostAsJsonAsync("/api/v1/admin/procedimentos", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("30715013", doc.RootElement.GetProperty("codigoTuss").GetString());
    }

    [Fact]
    public async Task POST_Procedimentos_CodigoTussDuplicado_Retorna409Async()
    {
        using var client = CreateAuthenticatedClient();
        var body = new
        {
            codigoTuss = "40314340",
            descricao = "Eletroencefalograma",
            ehSadt = true,
            temPorteProprioVideo = false,
            ativo = true,
        };

        await client.PostAsJsonAsync("/api/v1/admin/procedimentos", body);
        var response = await client.PostAsJsonAsync("/api/v1/admin/procedimentos", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GET_ProcedimentoPorId_NaoEncontrado_Retorna404Async()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/admin/procedimentos/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Procedimento_AlteraFlags_Retorna200ComNovoValorAsync()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/procedimentos",
            new
            {
                codigoTuss = "50000099",
                descricao = "Proc PUT",
                ehSadt = false,
                temPorteProprioVideo = false,
                ativo = true,
            });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var updateBody = new
        {
            codigoTuss = "50000099",
            descricao = "Proc PUT Atualizado",
            porte = "6B",
            porteAnestesico = 3,
            ehSadt = true,
            temPorteProprioVideo = false,
            ativo = true,
        };
        var response = await client.PutAsJsonAsync($"/api/v1/admin/procedimentos/{id}", updateBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Proc PUT Atualizado", doc.RootElement.GetProperty("descricao").GetString());
        Assert.True(doc.RootElement.GetProperty("ehSadt").GetBoolean());
    }

    [Fact]
    public async Task DELETE_Procedimento_Existente_Retorna204Async()
    {
        using var client = CreateAuthenticatedClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/procedimentos",
            new
            {
                codigoTuss = "99999001",
                descricao = "Para deletar",
                ehSadt = false,
                temPorteProprioVideo = false,
                ativo = true,
            });
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        var id = createDoc.RootElement.GetProperty("id").GetGuid();

        var response = await client.DeleteAsync(
            new Uri($"/api/v1/admin/procedimentos/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task POST_ImportarCsv_ArquivoValido_Retorna200ComResumoAsync()
    {
        using var client = CreateAuthenticatedClient();
        var csv = "CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo\n" +
                  "11111001;Proc CSV 1;;;false;false\n" +
                  "11111002;Proc CSV 2;6B;4;false;false\n";
        var csvBytes = Encoding.UTF8.GetBytes(csv);
        using var byteContent = new ByteArrayContent(csvBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(byteContent, "file", "procedimentos.csv");

        var response = await client.PostAsync(
            new Uri("/api/v1/admin/procedimentos/importar-csv", UriKind.Relative), multipart);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(2, doc.RootElement.GetProperty("inseridos").GetInt32());
    }

    [Fact]
    public async Task POST_ImportarCsv_ExtensaoErrada_Retorna400Async()
    {
        using var client = CreateAuthenticatedClient();
        var textBytes = Encoding.UTF8.GetBytes("conteudo qualquer");
        using var byteContent = new ByteArrayContent(textBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(byteContent, "file", "arquivo.txt");

        var response = await client.PostAsync(
            new Uri("/api/v1/admin/procedimentos/importar-csv", UriKind.Relative), multipart);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_ImportarCsv_ArquivoAcima5MB_Retorna400Async()
    {
        using var client = CreateAuthenticatedClient();
        using var largeContent = new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]);
        largeContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(largeContent, "file", "grande.csv");

        var response = await client.PostAsync(
            new Uri("/api/v1/admin/procedimentos/importar-csv", UriKind.Relative), multipart);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

file static class TestProcedimentoIdentity
{
    internal static readonly Guid TenantId = Guid.NewGuid();
    internal static readonly Guid UserId = Guid.NewGuid();
}

#pragma warning disable CA1812
file sealed class TestProcedimentoAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "TestProcedimento";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth-Proc"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestProcedimentoIdentity.UserId.ToString()),
            new Claim(ClaimTypes.Role, "TenantAdmin"),
            new Claim("role", "TenantAdmin"),
            new Claim("tenant_id", TestProcedimentoIdentity.TenantId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#pragma warning restore CA1812
