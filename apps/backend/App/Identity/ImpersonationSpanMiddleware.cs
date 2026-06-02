using System.Diagnostics;
using System.Security.Claims;

namespace App.Identity;

internal sealed class ImpersonationSpanMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.FindFirstValue("act_as_saas") == "true")
        {
            var tenantId = ctx.User.FindFirstValue("tenant_id");
            Activity.Current?.SetTag("saas.acting_tenant", tenantId);
        }

        await next(ctx);
    }
}
