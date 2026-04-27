using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Identity.Tests.CurrentUser;

internal sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }

    internal static FakeHttpContextAccessor ForSaasAdmin(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "SaasAdmin"),
            ],
            authenticationType: "Test");
        return new FakeHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    internal static FakeHttpContextAccessor ForTenantAdmin(Guid userId, Guid tenantId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "TenantAdmin"),
                new Claim("tenant_id", tenantId.ToString()),
            ],
            authenticationType: "Test");
        return new FakeHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    internal static FakeHttpContextAccessor ForMedico(Guid userId, Guid tenantId, Guid medicoId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "Medico"),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("medico_id", medicoId.ToString()),
            ],
            authenticationType: "Test");
        return new FakeHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    internal static FakeHttpContextAccessor ForUnauthenticated()
    {
        return new FakeHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
        };
    }
}
