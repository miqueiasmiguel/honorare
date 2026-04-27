using System.Security.Claims;
using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Identity;

internal sealed class TenantStatusMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && !ctx.User.IsInRole("SaasAdmin"))
        {
            var tenantIdStr = ctx.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantIdStr, out var tenantId))
            {
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.Id == tenantId, ctx.RequestAborted);

                if (tenant is null || tenant.Status != TenantStatus.Ativo)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsJsonAsync(
                        new { error = "tenant_suspended" }, ctx.RequestAborted);
                    return;
                }
            }
        }

        await next(ctx);
    }
}
