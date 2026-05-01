using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Identity;

internal sealed record ProfileSummary(Guid Id, string Email, string? Nome, string Role);

internal sealed class AdminService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    // ApplicationUser não implementa ITenantEntity; filtrar por TenantId explicitamente.
    internal async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId!.Value;

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

        return users
            .Select(u => new UserSummary(
                u.Id,
                u.Email!,
                u.Nome,
                AuthService.DeriveRole(u),
                u.IsActive,
                u.CreatedAt,
                u.MedicoId))
            .ToList();
    }

    // Rejeita auto-desativação para prevenir lockout do TenantAdmin.
    internal async Task<Result> UpdateUserStatusAsync(
        Guid userId, bool isActive, CancellationToken ct = default)
    {
        if (!isActive && userId == _currentUser.UserId)
        {
            return Result.Fail(new ForbiddenError("Você não pode desativar sua própria conta."));
        }

        var tenantId = _currentUser.TenantId!.Value;
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

    internal async Task<Result<ProfileSummary>> GetProfileAsync(CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([_currentUser.UserId], ct);
        if (user is null)
        {
            return Result<ProfileSummary>.Fail(new NotFoundError("Usuário não encontrado."));
        }

        var role = AuthService.DeriveRole(user);
        return Result<ProfileSummary>.Ok(new ProfileSummary(user.Id, user.Email!, user.Nome, role));
    }

    internal async Task<Result<ProfileSummary>> UpdateProfileAsync(
        string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<ProfileSummary>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (nome.Trim().Length > 100)
        {
            return Result<ProfileSummary>.Fail(new ValidationError("Nome deve ter no máximo 100 caracteres."));
        }

        var user = await _db.Users.FindAsync([_currentUser.UserId], ct);
        if (user is null)
        {
            return Result<ProfileSummary>.Fail(new NotFoundError("Usuário não encontrado."));
        }

        user.UpdateNome(nome);
        await _db.SaveChangesAsync(ct);

        var role = AuthService.DeriveRole(user);
        return Result<ProfileSummary>.Ok(new ProfileSummary(user.Id, user.Email!, user.Nome, role));
    }
}
