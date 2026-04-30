using App;
using App.Data;
using App.Identity;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public sealed class SaasServiceCreateTenantTests(PostgresContainerFixture db)
{
    private AppDbContext CreateContext() =>
        new(db.BuildOptions<AppDbContext>(), new SaasAdminCurrentUser());

    [Fact]
    public async Task CreateTenant_WithValidData_CreatesTenantAndOwnerInSameTransactionAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);
        var ownerEmail = $"owner-{Guid.NewGuid()}@alpha.com";

        var result = await svc.CreateTenantAsync("Clínica Alpha", ownerEmail);

        Assert.True(result.IsSuccess);
        var summary = result.Value!;

        var tenant = await ctx.Tenants.FindAsync(summary.TenantId);
        var owner = await ctx.Users.FindAsync(summary.OwnerId);
        Assert.NotNull(tenant);
        Assert.NotNull(owner);
        Assert.Equal(summary.TenantId, owner!.TenantId);
    }

    [Fact]
    public async Task CreateTenant_WithEmptyTenantName_ReturnsValidationErrorAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        var result = await svc.CreateTenantAsync("", "valid@example.com");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateTenant_WithEmptyOwnerEmail_ReturnsValidationErrorAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        var result = await svc.CreateTenantAsync("Tenant X", "");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateOwnerEmail_ReturnsConflictErrorAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);
        var email = $"dup-{Guid.NewGuid()}@example.com";

        await svc.CreateTenantAsync("Primeiro Tenant", email);
        var result = await svc.CreateTenantAsync("Segundo Tenant", email);

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task CreateTenant_OwnerHasTenantAdminRoleAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);
        var ownerEmail = $"admin-{Guid.NewGuid()}@example.com";

        var result = await svc.CreateTenantAsync("Tenant Y", ownerEmail);

        Assert.True(result.IsSuccess);
        var owner = await ctx.Users.FindAsync(result.Value!.OwnerId);
        Assert.NotNull(owner);
        Assert.Null(owner!.MedicoId);
        Assert.Equal(result.Value.TenantId, owner.TenantId);
        Assert.Equal("TenantAdmin", AuthService.DeriveRole(owner));
    }

    private sealed class SaasAdminCurrentUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => null;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => true;
        public bool IsAuthenticated => true;
    }
}
