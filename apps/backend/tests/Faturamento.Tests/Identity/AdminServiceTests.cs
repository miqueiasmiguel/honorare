using App;
using App.Data;
using App.Identity;
using Faturamento.Tests.Fixtures;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public sealed class AdminServiceTests(PostgresContainerFixture db)
{
    private AppDbContext BuildContext(ICurrentUser user) =>
        new(db.BuildOptions<AppDbContext>(), user);

    private static async Task<(Tenant tenant, ApplicationUser admin, ApplicationUser medico)>
        SeedTenantAsync(AppDbContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync();

        var tenant = Tenant.Create("Tenant Teste");
        ctx.Tenants.Add(tenant);

        var adminEmail = $"admin-{Guid.NewGuid()}@test.com";
        var medicoEmail = $"medico-{Guid.NewGuid()}@test.com";

        var admin = ApplicationUser.Create(adminEmail, tenant.Id, medicoId: null);
        var medico = ApplicationUser.Create(medicoEmail, tenant.Id, medicoId: Guid.NewGuid());

        ctx.Users.Add(admin);
        ctx.Users.Add(medico);
        await ctx.SaveChangesAsync();

        return (tenant, admin, medico);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsOnlyUsersOfCurrentTenantAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenantA, adminA, _) = await SeedTenantAsync(ctx);

        var tenantB = Tenant.Create("Outro Tenant");
        ctx.Tenants.Add(tenantB);
        var userB = ApplicationUser.Create($"outro-{Guid.NewGuid()}@test.com", tenantB.Id, null);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUser(adminA.Id, tenantA.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var users = await service.GetUsersAsync();

        Assert.Equal(2, users.Count); // admin + medico do tenantA
        Assert.DoesNotContain(users, u => u.Id == userB.Id);
    }

    [Fact]
    public async Task GetUsersAsync_DerivedRoleIsCorrectAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, medico) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var users = await service.GetUsersAsync();

        Assert.Contains(users, u => u.Id == admin.Id && u.Role == "TenantAdmin");
        Assert.Contains(users, u => u.Id == medico.Id && u.Role == "Medico");
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivatesMedicoSuccessfullyAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, medico) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(medico.Id, isActive: false);

        Assert.True(result.IsSuccess);
        await using var ctx3 = BuildContext(currentUser);
        var updated = await ctx3.Users.FindAsync([medico.Id]);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_SelfDeactivation_ReturnsForbiddenAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(admin.Id, isActive: false);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_UserFromOtherTenant_ReturnsNotFoundAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenantA, adminA, _) = await SeedTenantAsync(ctx);

        var tenantB = Tenant.Create("Tenant B");
        ctx.Tenants.Add(tenantB);
        var userB = ApplicationUser.Create($"b-{Guid.NewGuid()}@test.com", tenantB.Id, null);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUser(adminA.Id, tenantA.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(userB.Id, isActive: false);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsCurrentUserProfileAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.GetProfileAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(admin.Id, result.Value!.Id);
        Assert.Equal(admin.Email, result.Value.Email);
        Assert.Equal("TenantAdmin", result.Value.Role);
        Assert.Null(result.Value.Nome);
    }

    [Fact]
    public async Task UpdateProfileAsync_PersistsNomeAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync("  Dr. Exemplo  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Dr. Exemplo", result.Value!.Nome); // trim aplicado

        await using var ctx3 = BuildContext(currentUser);
        var refreshed = await ctx3.Users.FindAsync([admin.Id]);
        Assert.Equal("Dr. Exemplo", refreshed!.Nome);
    }

    [Fact]
    public async Task UpdateProfileAsync_NomeVazio_ReturnsValidationErrorAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync("   ");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_NomeMuitoLongo_ReturnsValidationErrorAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync(new string('a', 101));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_ActivatesInactiveUserSuccessfullyAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, medico) = await SeedTenantAsync(ctx);

        // Desativar primeiro para garantir estado inicial inativo
        medico.Deactivate();
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(medico.Id, isActive: true);

        Assert.True(result.IsSuccess);
        await using var ctx3 = BuildContext(currentUser);
        var updated = await ctx3.Users.FindAsync([medico.Id]);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task UpdateProfileAsync_NomeExatamente100Caracteres_SucceedsAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync(new string('a', 100));

        Assert.True(result.IsSuccess);
        Assert.Equal(new string('a', 100), result.Value!.Nome);
    }

    [Fact]
    public async Task GetUsersAsync_RetornaUsuariosOrdenadosPorCreatedAtAsync()
    {
        await using var ctx = BuildContext(new FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        // Criar terceiro usuário após um intervalo garantido
        await Task.Delay(10);
        var ultimo = ApplicationUser.Create($"ultimo-{Guid.NewGuid()}@test.com", tenant.Id, null);
        ctx.Users.Add(ultimo);
        await ctx.SaveChangesAsync();

        var currentUser = new FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var users = await service.GetUsersAsync();

        var datas = users.Select(u => u.CreatedAt).ToList();
        Assert.Equal(datas.OrderBy(d => d).ToList(), datas);
    }
}

file sealed class FakeCurrentUser(Guid userId, Guid tenantId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserId => userId;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
    public bool IsImpersonating => false;
}
