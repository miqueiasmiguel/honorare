using App;
using App.Data;
using App.Identity;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public sealed class TenantSettingsServiceTests(PostgresContainerFixture db)
{
    private AppDbContext BuildContext(ICurrentUser user) =>
        new(db.BuildOptions<AppDbContext>(), user);

    private static async Task<Tenant> SeedTenantAsync(AppDbContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync();

        var tenant = Tenant.Create("Clínica Teste");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        return tenant;
    }

    [Fact]
    public async Task GetSettingsAsync_DeveRetornarNomeEHasLogoFalseAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser);

        var result = await service.GetSettingsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(tenant.Id, result.Value!.Id);
        Assert.Equal("Clínica Teste", result.Value.Name);
        Assert.False(result.Value.HasLogo);
    }

    [Fact]
    public async Task RenameAsync_DeveAtualizarNomeDoTenantAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser);

        var result = await service.RenameAsync("Novo Nome");

        Assert.True(result.IsSuccess);
        Assert.Equal("Novo Nome", result.Value!.Name);

        await using var verifyCtx = BuildContext(currentUser);
        var updated = await verifyCtx.Tenants.FindAsync([tenant.Id]);
        Assert.Equal("Novo Nome", updated!.Name);
    }

    [Fact]
    public async Task RenameAsync_DeveFalharQuandoNomeEhVazioAsync()
    {
        await using var seedCtx = BuildContext(new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid()));
        var tenant = await SeedTenantAsync(seedCtx);

        var currentUser = new FakeTenantUser(Guid.NewGuid(), tenant.Id);
        await using var ctx = BuildContext(currentUser);
        var service = new TenantSettingsService(ctx, currentUser);

        var result = await service.RenameAsync("   ");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task RenameAsync_DeveFalharQuandoTenantNaoExisteAsync()
    {
        var currentUser = new FakeTenantUser(Guid.NewGuid(), Guid.NewGuid());
        await using var ctx = BuildContext(currentUser);
        await ctx.Database.EnsureCreatedAsync();
        var service = new TenantSettingsService(ctx, currentUser);

        var result = await service.RenameAsync("Qualquer Nome");

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }
}

file sealed class FakeTenantUser(Guid userId, Guid tenantId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserId => userId;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
}
