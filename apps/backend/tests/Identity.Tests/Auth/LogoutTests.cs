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
public class LogoutTests(PostgresContainerFixture db)
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

    private static string HashRaw(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    // Creates a user with an active tenant and logs them in once.
    private static async Task<(ApplicationUser user, string rawRefreshToken)> LoginUserAsync(
        AppDbContext ctx, AuthService service)
    {
        var email = $"{Guid.NewGuid()}@logout.test";
        var googleId = $"google-logout-{Guid.NewGuid()}";

        var tenant = Tenant.Create("Logout Tenant");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        user.AssociateGoogleId(googleId);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var result = await service.ProcessGoogleCallbackAsync(googleId, email);
        Assert.True(result.IsSuccess);
        return (user, result.Value!.RefreshToken);
    }

    // ── revoke all active tokens ────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_WithActiveTokens_RevokesAllTokensAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, _) = await LoginUserAsync(ctx, service);

        // Simulate a second device login
        var second = await service.ProcessGoogleCallbackAsync(user.GoogleId!, user.Email!);
        Assert.True(second.IsSuccess);

        await service.LogoutAsync(user.Id);

        ctx.ChangeTracker.Clear();
        var tokens = await ctx.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }

    // ── no tokens ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_WithNoActiveTokens_CompletesWithoutErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var email = $"{Guid.NewGuid()}@nologout.test";
        var tenant = Tenant.Create("No Token Tenant");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create(email, tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() => service.LogoutAsync(user.Id));
        Assert.Null(exception);
    }

    // ── refresh after logout returns 401 ────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_AfterLogout_RefreshTokenReturnsUnauthorizedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken) = await LoginUserAsync(ctx, service);

        await service.LogoutAsync(user.Id);
        ctx.ChangeTracker.Clear();

        var result = await service.RefreshTokenAsync(rawToken);

        Assert.True(result.IsFailure);
        Assert.IsType<UnauthorizedError>(result.Error);
    }

    // ── already-revoked tokens do not cause errors ───────────────────────────────

    [Fact]
    public async Task LogoutAsync_WithMixedRevokedAndActiveTokens_RevokesAllActiveAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var (user, rawToken1) = await LoginUserAsync(ctx, service);
        var second = await service.ProcessGoogleCallbackAsync(user.GoogleId!, user.Email!);
        Assert.True(second.IsSuccess);

        // Manually revoke first token so one is revoked, one is active
        var token1 = await ctx.RefreshTokens
            .FirstAsync(t => t.UserId == user.Id && t.TokenHash == HashRaw(rawToken1));
        token1.Revoke();
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // LogoutAsync must not throw even though one token is already revoked
        var exception = await Record.ExceptionAsync(() => service.LogoutAsync(user.Id));
        Assert.Null(exception);

        ctx.ChangeTracker.Clear();
        var allTokens = await ctx.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .ToListAsync();
        Assert.All(allTokens, t => Assert.True(t.IsRevoked));
    }
}
