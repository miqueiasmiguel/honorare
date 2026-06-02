using System.IdentityModel.Tokens.Jwt;
using App;
using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Auth;

[Collection(nameof(IdentityPostgresCollection))]
public class ImpersonationTests(PostgresContainerFixture db)
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

    private static Dictionary<string, string> DecodeJwtClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        return jwt.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value);
    }

    private static async Task<ApplicationUser> CreateSaasAdminAsync(AppDbContext ctx)
    {
        var email = $"{Guid.NewGuid()}@saas-imp.test";
        var user = ApplicationUser.Create(email); // no TenantId = SaasAdmin
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    // ── Impersonar_RetornaTokenComTenantEMarker ─────────────────────────────────

    [Fact]
    public async Task Impersonar_RetornaTokenComTenantEMarkerAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var tenant = Tenant.Create("Tenant Alvo");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var result = await service.CreateImpersonationTokensAsync(saasUser.Id, tenant.Id);

        Assert.True(result.IsSuccess);
        var claims = DecodeJwtClaims(result.Value!.AccessToken);
        Assert.Equal(saasUser.Id.ToString(), claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("SaasAdmin", claims["role"]);
        Assert.Equal(tenant.Id.ToString(), claims["tenant_id"]);
        Assert.Equal("true", claims["act_as_saas"]);
    }

    // ── Impersonar_PersisteRefreshComActingTenantId ─────────────────────────────

    [Fact]
    public async Task Impersonar_PersisteRefreshComActingTenantIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var tenant = Tenant.Create("Tenant Refresh");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var result = await service.CreateImpersonationTokensAsync(saasUser.Id, tenant.Id);

        Assert.True(result.IsSuccess);

        ctx.ChangeTracker.Clear();
        var refreshToken = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == saasUser.Id);

        Assert.NotNull(refreshToken);
        Assert.Equal(tenant.Id, refreshToken!.ActingTenantId);
    }

    // ── Impersonar_TenantInexistente_Falha ─────────────────────────────────────

    [Fact]
    public async Task Impersonar_TenantInexistente_FalhaAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var nonExistentTenantId = Guid.NewGuid();

        var result = await service.CreateImpersonationTokensAsync(saasUser.Id, nonExistentTenantId);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    // ── Impersonar_UsuarioNaoSaas_Falha ────────────────────────────────────────

    [Fact]
    public async Task Impersonar_UsuarioNaoSaas_FalhaAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var tenant = Tenant.Create("Tenant TenantAdmin");
        ctx.Tenants.Add(tenant);
        var tenantAdmin = ApplicationUser.Create($"{Guid.NewGuid()}@admin.test", tenant.Id);
        ctx.Users.Add(tenantAdmin);
        await ctx.SaveChangesAsync();

        var result = await service.CreateImpersonationTokensAsync(tenantAdmin.Id, tenant.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    // ── Impersonar_TenantSuspenso_PermiteToken ─────────────────────────────────

    [Fact]
    public async Task Impersonar_TenantSuspenso_PermiteTokenAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var saasUser = await CreateSaasAdminAsync(ctx);
        var tenant = Tenant.Create("Tenant Suspenso Imp");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var result = await service.CreateImpersonationTokensAsync(saasUser.Id, tenant.Id);

        Assert.True(result.IsSuccess);
    }
}
