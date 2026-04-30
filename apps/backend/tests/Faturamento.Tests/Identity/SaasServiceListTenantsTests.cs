using App.Data;
using App.Identity;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public sealed class SaasServiceListTenantsTests(PostgresContainerFixture db)
{
    private AppDbContext CreateContext() =>
        new(db.BuildOptions<AppDbContext>(), new SaasAdminCurrentUser());

    // DB is shared — never assert empty; verify subsets by known IDs.
    [Fact]
    public async Task ListTenants_WithNoTenants_ReturnsEmptyListAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        var result = await svc.ListTenantsAsync();

        Assert.NotNull(result);
        var neverCreatedId = Guid.NewGuid();
        Assert.DoesNotContain(neverCreatedId, result.Select(t => t.Id));
    }

    [Fact]
    public async Task ListTenants_ReturnsTenantWithCorrectAdminCountAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        // CreateTenantAsync creates the tenant + 1 admin owner atomically
        var ownerEmail = $"admin-count-{Guid.NewGuid()}@test.com";
        var createResult = await svc.CreateTenantAsync("Tenant Admin Count", ownerEmail);
        Assert.True(createResult.IsSuccess);
        var tenantId = createResult.Value!.TenantId;

        var list = await svc.ListTenantsAsync();
        var summary = list.First(t => t.Id == tenantId);

        Assert.Equal(1, summary.TotalAdmins);
        Assert.Equal(0, summary.TotalMedicos);
    }

    [Fact]
    public async Task ListTenants_ReturnsTenantWithCorrectMedicoCountAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        var ownerEmail = $"owner-medico-{Guid.NewGuid()}@test.com";
        var createResult = await svc.CreateTenantAsync("Tenant Medico Count", ownerEmail);
        Assert.True(createResult.IsSuccess);
        var tenantId = createResult.Value!.TenantId;

        var medicoEmail = $"medico-{Guid.NewGuid()}@test.com";
        var userResult = await svc.CreateUserAsync(tenantId, medicoEmail, "Medico", Guid.NewGuid());
        Assert.True(userResult.IsSuccess);

        var list = await svc.ListTenantsAsync();
        var summary = list.First(t => t.Id == tenantId);

        Assert.Equal(1, summary.TotalAdmins);
        Assert.Equal(1, summary.TotalMedicos);
    }

    [Fact]
    public async Task ListTenants_CountsAreIndependentPerTenantAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var svc = new SaasService(ctx);

        // Tenant A: 1 owner (admin) + 2 médicos
        var tenantAResult = await svc.CreateTenantAsync("Tenant A Indep", $"owner-a-{Guid.NewGuid()}@test.com");
        Assert.True(tenantAResult.IsSuccess);
        var tenantAId = tenantAResult.Value!.TenantId;

        await svc.CreateUserAsync(tenantAId, $"medico-a1-{Guid.NewGuid()}@test.com", "Medico", Guid.NewGuid());
        await svc.CreateUserAsync(tenantAId, $"medico-a2-{Guid.NewGuid()}@test.com", "Medico", Guid.NewGuid());

        // Tenant B: 1 owner (admin) + 1 admin extra
        var tenantBResult = await svc.CreateTenantAsync("Tenant B Indep", $"owner-b-{Guid.NewGuid()}@test.com");
        Assert.True(tenantBResult.IsSuccess);
        var tenantBId = tenantBResult.Value!.TenantId;

        await svc.CreateUserAsync(tenantBId, $"admin-b2-{Guid.NewGuid()}@test.com", "TenantAdmin", null);

        var list = await svc.ListTenantsAsync();

        var summaryA = list.First(t => t.Id == tenantAId);
        Assert.Equal(1, summaryA.TotalAdmins);
        Assert.Equal(2, summaryA.TotalMedicos);

        var summaryB = list.First(t => t.Id == tenantBId);
        Assert.Equal(2, summaryB.TotalAdmins);
        Assert.Equal(0, summaryB.TotalMedicos);
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
