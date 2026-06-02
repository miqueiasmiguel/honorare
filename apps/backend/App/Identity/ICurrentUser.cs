namespace App.Identity;

internal interface ICurrentUser
{
    Guid UserId { get; }
    Guid? TenantId { get; }
    Guid? MedicoId { get; }
    bool IsSaasAdmin { get; }
    bool IsImpersonating { get; }
    bool IsAuthenticated { get; }
}
