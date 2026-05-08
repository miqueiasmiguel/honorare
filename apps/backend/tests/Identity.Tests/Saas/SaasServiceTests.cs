using System.IdentityModel.Tokens.Jwt;
using App;
using App.Data;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests.Saas;

[Collection(nameof(IdentityPostgresCollection))]
public class SaasServiceTests(PostgresContainerFixture db)
{
    private static SaasService CreateService(AppDbContext ctx) => new(ctx);

    private static IConfiguration CreateAuthConfig() =>
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

    private static AuthService CreateAuthService(AppDbContext ctx) =>
        new(ctx, CreateAuthConfig());

    // ── ListTenants ─────────────────────────────────────────────────────────────
    // Container compartilhado: não é possível garantir DB vazio; testes validam subconjunto.

    [Fact]
    public async Task ListTenantsAsync_WithTenants_ReturnsAllTenantsWithCorrectFieldsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenantA = Tenant.Create($"Clínica A {Guid.NewGuid()}");
        var tenantB = Tenant.Create($"Clínica B {Guid.NewGuid()}");
        tenantB.Suspend();
        ctx.Tenants.Add(tenantA);
        ctx.Tenants.Add(tenantB);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ListTenantsAsync();

        var a = result.First(t => t.Id == tenantA.Id);
        var b = result.First(t => t.Id == tenantB.Id);

        Assert.Equal(tenantA.Name, a.Name);
        Assert.Equal(TenantStatus.Ativo, a.Status);
        Assert.Equal(TenantStatus.Suspenso, b.Status);
        Assert.True(a.CreatedAt > DateTimeOffset.MinValue);
    }

    // ── CreateTenant ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTenantAsync_ValidData_ReturnsTenantWithOwnerSummaryAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);
        var ownerEmail = $"owner-{Guid.NewGuid()}@test.com";

        var result = await service.CreateTenantAsync($"Clínica {Guid.NewGuid()}", ownerEmail);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Ativo, result.Value!.Status);
        Assert.NotEqual(Guid.Empty, result.Value!.TenantId);
        Assert.True(result.Value!.CreatedAt > DateTimeOffset.MinValue);
        Assert.Equal(ownerEmail, result.Value!.OwnerEmail);
    }

    [Fact]
    public async Task CreateTenantAsync_EmptyName_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.CreateTenantAsync(string.Empty, "owner@test.com");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateTenantAsync_WhitespaceName_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.CreateTenantAsync("   ", "owner@test.com");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    // ── UpdateTenantStatus ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTenantStatusAsync_TenantNotFound_ReturnsNotFoundErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.UpdateTenantStatusAsync(Guid.NewGuid(), "Suspenso");

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task UpdateTenantStatusAsync_SuspendActiveTenant_ReturnsUpdatedSummaryWithSuspensoAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Suspender {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateTenantStatusAsync(tenant.Id, "Suspenso");

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Suspenso, result.Value!.Status);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.Tenants.FindAsync(tenant.Id);
        Assert.Equal(TenantStatus.Suspenso, stored!.Status);
    }

    [Fact]
    public async Task UpdateTenantStatusAsync_CancelActiveTenant_ReturnsUpdatedSummaryWithCanceladoAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Cancelar {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateTenantStatusAsync(tenant.Id, "Cancelado");

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Cancelado, result.Value!.Status);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.Tenants.FindAsync(tenant.Id);
        Assert.Equal(TenantStatus.Cancelado, stored!.Status);
    }

    [Fact]
    public async Task UpdateTenantStatusAsync_ActivateSuspendedTenant_ReturnsUpdatedSummaryWithAtivoAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Reativar {Guid.NewGuid()}");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateTenantStatusAsync(tenant.Id, "Ativo");

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Ativo, result.Value!.Status);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.Tenants.FindAsync(tenant.Id);
        Assert.Equal(TenantStatus.Ativo, stored!.Status);
    }

    [Fact]
    public async Task UpdateTenantStatusAsync_InvalidStatus_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Inválido {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateTenantStatusAsync(tenant.Id, "StatusInexistente");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    // ── ListTenantUsers ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTenantUsersAsync_TenantNotFound_ReturnsNotFoundErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.ListTenantUsersAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task ListTenantUsersAsync_ValidTenant_ReturnsOnlyUsersOfThatTenantAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"ListUsers {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        var user1 = ApplicationUser.Create($"u1-{Guid.NewGuid()}@list.test", tenant.Id);
        var user2 = ApplicationUser.Create($"u2-{Guid.NewGuid()}@list.test", tenant.Id);
        ctx.Users.Add(user1);
        ctx.Users.Add(user2);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ListTenantUsersAsync(tenant.Id);

        Assert.True(result.IsSuccess);
        var ids = result.Value!.Select(u => u.Id).ToHashSet();
        Assert.Contains(user1.Id, ids);
        Assert.Contains(user2.Id, ids);
    }

    [Fact]
    public async Task ListTenantUsersAsync_ValidTenant_DoesNotReturnUsersOfOtherTenantsAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenantA = Tenant.Create($"TenantA {Guid.NewGuid()}");
        var tenantB = Tenant.Create($"TenantB {Guid.NewGuid()}");
        ctx.Tenants.Add(tenantA);
        ctx.Tenants.Add(tenantB);
        var userA = ApplicationUser.Create($"ua-{Guid.NewGuid()}@sep.test", tenantA.Id);
        var userB = ApplicationUser.Create($"ub-{Guid.NewGuid()}@sep.test", tenantB.Id);
        ctx.Users.Add(userA);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.ListTenantUsersAsync(tenantA.Id);

        Assert.True(result.IsSuccess);
        var ids = result.Value!.Select(u => u.Id).ToHashSet();
        Assert.Contains(userA.Id, ids);
        Assert.DoesNotContain(userB.Id, ids);
    }

    // ── CreateUser ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_TenantNotFound_ReturnsNotFoundErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.CreateUserAsync(
            Guid.NewGuid(), $"{Guid.NewGuid()}@ghost.test", "TenantAdmin", null);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_EmailAlreadyInUse_ReturnsConflictErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"EmailConflict {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        var email = $"{Guid.NewGuid()}@conflict.test";
        ctx.Users.Add(ApplicationUser.Create(email, tenant.Id));
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(tenant.Id, email, "TenantAdmin", null);

        Assert.True(result.IsFailure);
        Assert.IsType<ConflictError>(result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_TenantAdminRole_CreatesUserWithoutMedicoIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"AdminRole {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@admin.test", "TenantAdmin", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("TenantAdmin", result.Value!.Role);
        Assert.Null(result.Value!.MedicoId);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
    }

    [Fact]
    public async Task CreateUserAsync_MedicoRoleWithMedicoId_CreatesUserWithMedicoIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"MedicoRole {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var medicoId = Guid.NewGuid();
        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@medico.test", "Medico", medicoId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Medico", result.Value!.Role);
        Assert.Equal(medicoId, result.Value!.MedicoId);
    }

    [Fact]
    public async Task CreateUserAsync_MedicoRoleWithoutMedicoId_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"MedicoNoId {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@medico.test", "Medico", null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_InvalidRole_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"InvalidRole {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@role.test", "Superuser", null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_SaasAdminRole_ReturnsValidationErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"SaasRole {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@saas.test", "SaasAdmin", null);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task CreateUserAsync_CreatedUser_IsActiveByDefaultAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"DefaultActive {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateUserAsync(
            tenant.Id, $"{Guid.NewGuid()}@active.test", "TenantAdmin", null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
    }

    // ── UpdateUserStatus ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserStatusAsync_TenantNotFound_ReturnsNotFoundErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var service = CreateService(ctx);

        var result = await service.UpdateUserStatusAsync(Guid.NewGuid(), Guid.NewGuid(), false);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_UserNotInTenant_ReturnsNotFoundErrorAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenantA = Tenant.Create($"TenantA {Guid.NewGuid()}");
        var tenantB = Tenant.Create($"TenantB {Guid.NewGuid()}");
        ctx.Tenants.Add(tenantA);
        ctx.Tenants.Add(tenantB);
        var userB = ApplicationUser.Create($"{Guid.NewGuid()}@wrong.test", tenantB.Id);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        // userB belongs to tenantB, not tenantA
        var result = await service.UpdateUserStatusAsync(tenantA.Id, userB.Id, false);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivateActiveUser_UserBecomesInactiveAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Deactivate {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create($"{Guid.NewGuid()}@deactivate.test", tenant.Id);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateUserStatusAsync(tenant.Id, user.Id, false);

        Assert.True(result.IsSuccess);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.Users.FindAsync(user.Id);
        Assert.False(stored!.IsActive);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_ActivateInactiveUser_UserBecomesActiveAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create($"Activate {Guid.NewGuid()}");
        ctx.Tenants.Add(tenant);
        var user = ApplicationUser.Create($"{Guid.NewGuid()}@activate.test", tenant.Id);
        user.Deactivate();
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.UpdateUserStatusAsync(tenant.Id, user.Id, true);

        Assert.True(result.IsSuccess);

        ctx.ChangeTracker.Clear();
        var stored = await ctx.Users.FindAsync(user.Id);
        Assert.True(stored!.IsActive);
    }

    // ── Critério de pronto: fluxo completo ──────────────────────────────────────

    [Fact]
    public async Task CreateUser_ThenAuthenticateViaGoogle_JwtContainsCorrectTenantIdAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var saasService = CreateService(ctx);
        var authService = CreateAuthService(ctx);

        // SaaS admin cria tenant e usuário via painel
        var tenantResult = await saasService.CreateTenantAsync(
            $"FluxoCompleto {Guid.NewGuid()}", $"owner-{Guid.NewGuid()}@fluxo.test");
        Assert.True(tenantResult.IsSuccess);
        var tenantId = tenantResult.Value!.TenantId;

        var email = $"{Guid.NewGuid()}@fluxo.test";
        var userResult = await saasService.CreateUserAsync(tenantId, email, "TenantAdmin", null);
        Assert.True(userResult.IsSuccess);

        // Usuário faz primeiro login via Google (associa GoogleId)
        var googleId = $"google-fluxo-{Guid.NewGuid()}";
        var authResult = await authService.ProcessGoogleCallbackAsync(googleId, email);

        Assert.True(authResult.IsSuccess);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(authResult.Value!.AccessToken);
        var claims = jwt.Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(tenantId.ToString(), claims["tenant_id"]);
        Assert.Equal("TenantAdmin", claims["role"]);
        Assert.DoesNotContain("medico_id", claims.Keys);
    }
}
