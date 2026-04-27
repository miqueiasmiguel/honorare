using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Identity;

internal sealed record TenantSummary(Guid Id, string Name, TenantStatus Status, DateTimeOffset CreatedAt);

internal sealed record UserSummary(
    Guid Id, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt, Guid? MedicoId);

internal sealed class SaasService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    internal async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.ToListAsync(ct);
        return tenants
            .Select(t => new TenantSummary(t.Id, t.Name, t.Status, t.CreatedAt))
            .ToList();
    }

    internal async Task<Result<TenantSummary>> CreateTenantAsync(
        string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<TenantSummary>.Fail(new ValidationError("Nome do tenant é obrigatório."));
        }

        var tenant = Tenant.Create(name.Trim());
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);
        return Result<TenantSummary>.Ok(new TenantSummary(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt));
    }

    internal async Task<Result<TenantSummary>> UpdateTenantStatusAsync(
        Guid tenantId, string status, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null)
        {
            return Result<TenantSummary>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        switch (status)
        {
            case "Ativo":
                tenant.Activate();
                break;
            case "Suspenso":
                tenant.Suspend();
                break;
            case "Cancelado":
                tenant.Cancel();
                break;
            default:
                return Result<TenantSummary>.Fail(
                    new ValidationError($"Status inválido: '{status}'. Use Ativo, Suspenso ou Cancelado."));
        }

        await _db.SaveChangesAsync(ct);
        return Result<TenantSummary>.Ok(new TenantSummary(tenant.Id, tenant.Name, tenant.Status, tenant.CreatedAt));
    }

    internal async Task<Result<IReadOnlyList<UserSummary>>> ListTenantUsersAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
        {
            return Result<IReadOnlyList<UserSummary>>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        // Materialize before projecting: DeriveRole cannot be translated to SQL
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(ct);

        IReadOnlyList<UserSummary> summaries = users
            .Select(u => new UserSummary(
                u.Id,
                u.Email!,
                AuthService.DeriveRole(u),
                u.IsActive,
                u.CreatedAt,
                u.MedicoId))
            .ToList();

        return Result<IReadOnlyList<UserSummary>>.Ok(summaries);
    }

    internal async Task<Result<UserSummary>> CreateUserAsync(
        Guid tenantId, string email, string role, Guid? medicoId, CancellationToken ct = default)
    {
        if (role is not ("TenantAdmin" or "Medico"))
        {
            return Result<UserSummary>.Fail(
                new ValidationError("Role inválido. Use TenantAdmin ou Medico."));
        }

        if (role == "Medico" && medicoId is null)
        {
            return Result<UserSummary>.Fail(
                new ValidationError("MedicoId é obrigatório para role Medico."));
        }

        var tenant = await _db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null)
        {
            return Result<UserSummary>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        var emailInUse = await _db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailInUse)
        {
            return Result<UserSummary>.Fail(new ConflictError("E-mail já está em uso."));
        }

        var resolvedMedicoId = role == "Medico" ? medicoId : null;
        var user = ApplicationUser.Create(email, tenantId, resolvedMedicoId);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var derivedRole = AuthService.DeriveRole(user);
        return Result<UserSummary>.Ok(new UserSummary(
            user.Id, user.Email!, derivedRole, user.IsActive, user.CreatedAt, user.MedicoId));
    }

    internal async Task<Result> UpdateUserStatusAsync(
        Guid tenantId, Guid userId, bool isActive, CancellationToken ct = default)
    {
        // Validate tenant exists (LGPD: auditabilidade do acesso)
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
        {
            return Result.Fail(new NotFoundError("Tenant não encontrado."));
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user is null)
        {
            return Result.Fail(new NotFoundError("Usuário não encontrado neste tenant."));
        }

        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
