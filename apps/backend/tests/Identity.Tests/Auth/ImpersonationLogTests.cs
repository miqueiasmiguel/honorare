using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Auth;

[Collection(nameof(IdentityPostgresCollection))]
public class ImpersonationLogTests(PostgresContainerFixture db)
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

    private static AuthService CreateService(AppDbContext ctx) => new(ctx, CreateConfig());

    private static async Task<ApplicationUser> CreateSaasAdminAsync(AppDbContext ctx)
    {
        var email = $"{Guid.NewGuid()}@log-test.test";
        var user = ApplicationUser.Create(email);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    // ── Impersonar_GravaLogComStartedAt ────────────────────────────────────────

    [Fact]
    public async Task Impersonar_GravaLogComStartedAtAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var tenant = Tenant.Create("Tenant Log");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var result = await service.CreateImpersonationTokensAsync(saasUser.Id, tenant.Id);
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);

        ctx.ChangeTracker.Clear();
        var log = await ctx.ImpersonationLogs
            .FirstOrDefaultAsync(l => l.SaasUserId == saasUser.Id && l.TenantId == tenant.Id);

        Assert.NotNull(log);
        Assert.InRange(log!.StartedAt, before, after);
        Assert.Null(log.EndedAt);
    }

    // ── Exit_RevogaRefreshDeImpersonacao_EFechaLog ─────────────────────────────

    [Fact]
    public async Task Exit_RevogaRefreshDeImpersonacao_EFechaLogAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var tenant = Tenant.Create("Tenant Exit");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var impResult = await service.CreateImpersonationTokensAsync(saasUser.Id, tenant.Id);
        Assert.True(impResult.IsSuccess);

        var before = DateTimeOffset.UtcNow;
        await service.EndImpersonationAsync(saasUser.Id);
        var after = DateTimeOffset.UtcNow;

        ctx.ChangeTracker.Clear();

        // All impersonation refresh tokens must be revoked
        var impTokens = await ctx.RefreshTokens
            .Where(t => t.UserId == saasUser.Id && t.ActingTenantId != null)
            .ToListAsync();

        Assert.All(impTokens, t => Assert.True(t.IsRevoked));

        // Log must be closed
        var log = await ctx.ImpersonationLogs
            .FirstOrDefaultAsync(l => l.SaasUserId == saasUser.Id && l.TenantId == tenant.Id);

        Assert.NotNull(log);
        Assert.NotNull(log!.EndedAt);
        Assert.InRange(log.EndedAt!.Value, before, after);
    }
}
