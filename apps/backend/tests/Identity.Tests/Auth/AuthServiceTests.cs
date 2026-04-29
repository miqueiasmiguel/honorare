using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App;
using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Auth;

[Collection(nameof(IdentityPostgresCollection))]
public class AuthServiceTests(PostgresContainerFixture db)
{
    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-characters-ok",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            })
            .Build();

    private static AuthService CreateService(AppDbContext ctx) =>
        new(ctx, CreateConfig());

    private static Dictionary<string, string> DecodeJwtClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value);
    }

    // ── user-not-found ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_UserNotFound_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.ProcessGoogleCallbackAsync(
            "ghost-google-id", $"{Guid.NewGuid()}@unknown.test");

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── first login: GoogleId association ───────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_UserFoundByEmailWithoutGoogleId_AssociatesGoogleIdAndSucceedsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@firstlogin.test";
        var googleId = $"google-first-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Primeiro Login");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var stored = await ctx.Users.FindAsync(user.Id);
        Assert.Equal(googleId, stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_UserFoundByEmailWithExistingGoogleId_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@conflict.test";
        const string ExistingGoogleId = "existing-google-id-conflict";

        var tenant = Tenant.Create("Tenant Conflito");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(ExistingGoogleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // Different GoogleId trying to authenticate with the same email
        var result = await service.ProcessGoogleCallbackAsync("another-google-id", email);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── inactive user ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_InactiveUser_ReturnsForbiddenAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@inactive.test";
        var googleId = $"google-inactive-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Inativo");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(googleId);
        user.Deactivate();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    // ── suspended / cancelled tenant ────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_SuspendedTenant_ReturnsForbiddenAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@suspended.test";
        var googleId = $"google-suspended-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Suspenso");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_CancelledTenant_ReturnsForbiddenAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@cancelled.test";
        var googleId = $"google-cancelled-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Cancelado");
        tenant.Cancel();
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    // ── JWT claims per role ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_SaasAdmin_JwtHasRoleAndNoTenantIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@saas.test";
        var googleId = $"google-saas-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email);  // no tenantId = SaasAdmin
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal("SaasAdmin", claims[ClaimTypes.Role]);
        Assert.DoesNotContain("tenant_id", claims.Keys);
        Assert.DoesNotContain("medico_id", claims.Keys);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_TenantAdmin_JwtHasTenantIdAndNoMedicoIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@tenantadmin.test";
        var googleId = $"google-admin-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Admin Teste");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);  // medicoId = null = TenantAdmin
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal("TenantAdmin", claims[ClaimTypes.Role]);
        Assert.Equal(tenant.Id.ToString(), claims["tenant_id"]);
        Assert.DoesNotContain("medico_id", claims.Keys);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_Medico_JwtHasTenantIdAndMedicoIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@medico.test";
        var googleId = $"google-medico-{Guid.NewGuid()}";
        var medicoId = Guid.NewGuid();

        var tenant = Tenant.Create("Tenant Medico Teste");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id, medicoId);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal("Medico", claims[ClaimTypes.Role]);
        Assert.Equal(tenant.Id.ToString(), claims["tenant_id"]);
        Assert.Equal(medicoId.ToString(), claims["medico_id"]);
    }

    // ── JWT structure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_Success_JwtHasSubEmailAndJtiAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@structure.test";
        var googleId = $"google-structure-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal(user.Id.ToString(), claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal(email, claims[JwtRegisteredClaimNames.Email]);
        Assert.True(Guid.TryParse(claims[JwtRegisteredClaimNames.Jti], out _),
            "jti deve ser um Guid válido");
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_Success_ExpiresInIs900SecondsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@expiry.test";
        var googleId = $"google-expiry-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        Assert.Equal(900, result.Value!.ExpiresIn);  // 15 min * 60
    }

    // ── refresh token ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_Success_RefreshTokenStoredHashedNotRawAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@hash.test";
        var googleId = $"google-hash-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var rawToken = result.Value!.RefreshToken;

        var storedToken = ctx.RefreshTokens.First(t => t.UserId == user.Id);

        // The raw token must NOT appear in the DB
        Assert.NotEqual(rawToken, storedToken.TokenHash);
        Assert.False(storedToken.IsRevoked);
        Assert.True(storedToken.ExpiresAt > DateTimeOffset.UtcNow);

        // The stored hash must match SHA-256 of the raw token
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(expectedHash, storedToken.TokenHash, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_TwoCallsForSameUser_EachIssuesDistinctRefreshTokenAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@multi.test";
        var googleId = $"google-multi-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var r1 = await service.ProcessGoogleCallbackAsync(googleId, email);
        var r2 = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.NotEqual(r1.Value!.RefreshToken, r2.Value!.RefreshToken);
    }

    // ── TASK-AUTH-10: primeiro login (associação de GoogleId) ────────────────

    [Fact]
    public async Task ProcessGoogleCallbackAsync_SaasAdminPreCadastrado_AssociatesGoogleIdAndIssuesCorrectJwtAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@saas-precad.test";
        var googleId = $"google-saas-precad-{Guid.NewGuid()}";

        var user = ApplicationUser.Create(email); // SaasAdmin — sem TenantId
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal("SaasAdmin", claims[ClaimTypes.Role]);
        Assert.DoesNotContain("tenant_id", claims.Keys);
        Assert.DoesNotContain("medico_id", claims.Keys);

        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Equal(googleId, stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_MedicoPreCadastrado_AssociatesGoogleIdAndIssuesCorrectClaimsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@medico-precad.test";
        var googleId = $"google-medico-precad-{Guid.NewGuid()}";
        var medicoId = Guid.NewGuid();

        var tenant = Tenant.Create("Tenant Medico PreCad");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id, medicoId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal("Medico", claims[ClaimTypes.Role]);
        Assert.Equal(tenant.Id.ToString(), claims["tenant_id"]);
        Assert.Equal(medicoId.ToString(), claims["medico_id"]);

        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Equal(googleId, stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_FirstLogin_GoogleIdPersistedToFreshContextAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@persist-precad.test";
        var googleId = $"google-persist-precad-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Persist PreCad");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);
        Assert.True(result.IsSuccess);

        // Contexto completamente novo — não usa o identity map do ctx anterior
        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Equal(googleId, stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_FirstLoginAssociatesGoogleId_SubsequentLoginByGoogleIdSucceedsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@lifecycle-precad.test";
        var googleId = $"google-lifecycle-precad-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Lifecycle PreCad");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        // Primeiro login: entra pelo caminho de lookup por email
        var firstResult = await service.ProcessGoogleCallbackAsync(googleId, email);
        Assert.True(firstResult.IsSuccess);

        // Segundo login: agora o GoogleId está associado — entra pelo caminho do GoogleId
        var secondResult = await service.ProcessGoogleCallbackAsync(googleId, email);
        Assert.True(secondResult.IsSuccess);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_InactivePreCadastrado_GoogleIdNotPersistedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@inactive-precad.test";
        var googleId = $"google-inactive-precad-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Inativo PreCad");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.Deactivate();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);

        // Fresh context confirma que GoogleId NÃO foi persistido — SaveChangesAsync nunca foi chamado
        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Null(stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_SuspendedTenantPreCadastrado_GoogleIdNotPersistedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@susp-precad.test";
        var googleId = $"google-susp-precad-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Suspenso PreCad");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);

        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Null(stored!.GoogleId);
    }

    [Fact]
    public async Task ProcessGoogleCallbackAsync_CancelledTenantPreCadastrado_GoogleIdNotPersistedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var email = $"{Guid.NewGuid()}@cancel-precad.test";
        var googleId = $"google-cancel-precad-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Tenant Cancelado PreCad");
        tenant.Cancel();
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);

        await using var freshCtx = await db.CreateContextAsync();
        var stored = await freshCtx.Users.FindAsync(user.Id);
        Assert.Null(stored!.GoogleId);
    }
}
