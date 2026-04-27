using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App;
using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Auth;

[Collection(nameof(IdentityPostgresCollection))]
public class RefreshTokenTests(PostgresContainerFixture db)
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

    private static string HashRaw(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    // Creates a user with an active tenant and logs them in.
    private static async Task<(ApplicationUser user, string rawRefreshToken)> LoginUserAsync(
        AppDbContext ctx, AuthService service, Guid? medicoId = null)
    {
        var email = $"{Guid.NewGuid()}@refresh.test";
        var googleId = $"google-refresh-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Refresh Tenant");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id, medicoId);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.ProcessGoogleCallbackAsync(googleId, email);
        Assert.True(result.IsSuccess);
        return (user, result.Value!.RefreshToken);
    }

    // ── valid token ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewPairAndRevokesOldAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken) = await LoginUserAsync(ctx, service);

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.AccessToken);
        Assert.NotEmpty(result.Value!.RefreshToken);
        Assert.NotEqual(rawToken, result.Value!.RefreshToken);

        // Old token must be revoked
        ctx.ChangeTracker.Clear();
        var oldToken = await ctx.RefreshTokens
            .FirstAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(rawToken));
        Assert.True(oldToken.IsRevoked);

        // New token must exist and not be revoked
        var newToken = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(result.Value!.RefreshToken));
        Assert.NotNull(newToken);
        Assert.False(newToken!.IsRevoked);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_OldTokenLinkedToNewViaReplacedByTokenIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken) = await LoginUserAsync(ctx, service);

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsSuccess);

        ctx.ChangeTracker.Clear();
        var oldToken = await ctx.RefreshTokens
            .FirstAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(rawToken));
        var newToken = await ctx.RefreshTokens
            .FirstAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(result.Value!.RefreshToken));

        Assert.Equal(newToken.Id.ToString(), oldToken.ReplacedByTokenId);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_NewAccessTokenHasCorrectClaimsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var medicoId = Guid.NewGuid();
        var (user, rawToken) = await LoginUserAsync(ctx, service, medicoId);

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal(user.Id.ToString(), claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("Medico", claims[ClaimTypes.Role]);
        Assert.Equal(user.TenantId!.Value.ToString(), claims["tenant_id"]);
        Assert.Equal(medicoId.ToString(), claims["medico_id"]);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ExpiresInIs900SecondsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (_, rawToken) = await LoginUserAsync(ctx, service);

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(900, result.Value!.ExpiresIn);
    }

    // ── revoked token ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken) = await LoginUserAsync(ctx, service);

        var token = await ctx.RefreshTokens
            .FirstAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(rawToken));
        token.Revoke();
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── expired token ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var email = $"{Guid.NewGuid()}@expired.test";
        var googleId = $"google-exp-{Guid.NewGuid()}";
        var tenant = Tenant.Create("Expired Token Tenant");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiredToken = RefreshToken.Create(
            user.Id, HashRaw(rawToken), DateTimeOffset.UtcNow.AddMinutes(-1));
        ctx.RefreshTokens.Add(expiredToken);
        await ctx.SaveChangesAsync();

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── inactive user ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_InactiveUser_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken) = await LoginUserAsync(ctx, service);

        var storedUser = await ctx.Users.FindAsync(user.Id);
        storedUser!.Deactivate();
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── unknown token ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_UnknownToken_ReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.RefreshTokenAsync("completely-unknown-" + Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }
}
