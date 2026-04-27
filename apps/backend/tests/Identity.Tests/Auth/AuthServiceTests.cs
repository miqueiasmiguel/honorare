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
}
