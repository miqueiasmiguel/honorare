using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests.Schema;

[Collection(nameof(IdentityPostgresCollection))]
public sealed class IdentitySchemaTests(PostgresContainerFixture db)
{
    [Fact]
    public async Task ApplicationUser_WithNullTenantIdAndMedicoId_Persists_AsSaasAdmin_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var user = ApplicationUser.Create("saasadmin@honorare.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Users.FindAsync(user.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.TenantId);
        Assert.Null(saved.MedicoId);
    }

    [Fact]
    public async Task ApplicationUser_WithNullGoogleId_Persists_AsPreRegistered_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var user = ApplicationUser.Create("preCadastrado@clinic.com", Guid.NewGuid());
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Users.FindAsync(user.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.GoogleId);
    }

    [Fact]
    public async Task ApplicationUser_DuplicateGoogleId_ThrowsDbUpdateException_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var googleId = $"google-{Guid.NewGuid()}";

        var user1 = ApplicationUser.Create("user1@clinic.com", Guid.NewGuid());
        user1.AssociateGoogleId(googleId);
        var user2 = ApplicationUser.Create("user2@clinic.com", Guid.NewGuid());
        user2.AssociateGoogleId(googleId);

        ctx.Users.AddRange(user1, user2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task RefreshToken_DuplicateTokenHash_ThrowsDbUpdateException_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var user = ApplicationUser.Create("user@clinic.com", Guid.NewGuid());
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var hash = $"hash-{Guid.NewGuid()}";
        var token1 = RefreshToken.Create(user.Id, hash, DateTimeOffset.UtcNow.AddDays(7));
        var token2 = RefreshToken.Create(user.Id, hash, DateTimeOffset.UtcNow.AddDays(7));

        ctx.RefreshTokens.AddRange(token1, token2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_Persists_WithAllProperties_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create("Clínica Exemplo");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var saved = await ctx.Tenants.FindAsync(tenant.Id);
        Assert.NotNull(saved);
        Assert.Equal("Clínica Exemplo", saved.Name);
        Assert.Equal(TenantStatus.Ativo, saved.Status);
    }

    [Fact]
    public async Task ApplicationUser_QueryWithoutFilter_ReturnsBothTenants_Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        ctx.Users.AddRange(
            ApplicationUser.Create("a@t1.com", tenant1),
            ApplicationUser.Create("b@t2.com", tenant2));
        await ctx.SaveChangesAsync();

        var count = await ctx.Users
            .Where(u => u.TenantId == tenant1 || u.TenantId == tenant2)
            .CountAsync();
        Assert.Equal(2, count);
    }
}
