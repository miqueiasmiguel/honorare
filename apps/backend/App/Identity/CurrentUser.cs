using System.Security.Claims;

namespace App.Identity;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? MedicoId
    {
        get
        {
            var value = User?.FindFirstValue("medico_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsSaasAdmin => User?.IsInRole("SaasAdmin") ?? false;

    public bool IsImpersonating => IsSaasAdmin && TenantId is not null;
}
