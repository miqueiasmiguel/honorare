using Microsoft.AspNetCore.Identity;

namespace App.Identity;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    // PasswordHash é herdado de IdentityUser e sempre permanece nulo.
    // Auth é exclusivamente via Google OAuth — nenhuma senha é aceita ou persistida.
    public string? GoogleId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? MedicoId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ApplicationUser() { }

    public static ApplicationUser Create(string email, Guid? tenantId = null, Guid? medicoId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            TenantId = tenantId,
            MedicoId = medicoId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public Result AssociateGoogleId(string googleId)
    {
        if (GoogleId is not null)
        {
            return Result.Fail(new ConflictError("GoogleId já está associado a este usuário."));
        }
        GoogleId = googleId;
        return Result.Ok();
    }
}
