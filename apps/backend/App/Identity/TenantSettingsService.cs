using App.Data;

namespace App.Identity;

internal sealed record TenantSettings(Guid Id, string Name, bool HasLogo);

internal sealed class TenantSettingsService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    internal async Task<Result<TenantSettings>> GetSettingsAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        return Result<TenantSettings>.Ok(new TenantSettings(tenant.Id, tenant.Name, tenant.LogoKey is not null));
    }

    internal async Task<Result<TenantSettings>> RenameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<TenantSettings>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (name.Trim().Length > 256)
        {
            return Result<TenantSettings>.Fail(new ValidationError("Nome deve ter no máximo 256 caracteres."));
        }

        var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
        if (tenant is null)
        {
            return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
        }

        tenant.Rename(name);
        await _db.SaveChangesAsync(ct);

        return Result<TenantSettings>.Ok(new TenantSettings(tenant.Id, tenant.Name, tenant.LogoKey is not null));
    }
}
