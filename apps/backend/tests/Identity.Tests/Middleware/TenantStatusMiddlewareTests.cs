using System.Security.Claims;
using App.Identity;
using Identity.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace Identity.Tests.Middleware;

[Collection(nameof(IdentityPostgresCollection))]
public class TenantStatusMiddlewareTests(PostgresContainerFixture db)
{
    // Builds an authenticated HttpContext with the given tenant_id and role claims.
    private static DefaultHttpContext BuildAuthenticatedContext(
        Guid? tenantId = null,
        string role = "TenantAdmin")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };

        if (tenantId is not null)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static DefaultHttpContext BuildUnauthenticatedContext() => new();

    // ── active tenant ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ActiveTenant_CallsNextAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create("Active Tenant MW");
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(BuildAuthenticatedContext(tenant.Id), ctx);

        Assert.True(nextCalled);
    }

    // ── suspended tenant ────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SuspendedTenant_Returns403Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create("Suspended Tenant MW");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpCtx = BuildAuthenticatedContext(tenant.Id);
        await middleware.InvokeAsync(httpCtx, ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpCtx.Response.StatusCode);
    }

    // ── cancelled tenant ────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_CancelledTenant_Returns403Async()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create("Cancelled Tenant MW");
        tenant.Cancel();
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpCtx = BuildAuthenticatedContext(tenant.Id);
        await middleware.InvokeAsync(httpCtx, ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpCtx.Response.StatusCode);
    }

    // ── SaasAdmin bypasses middleware ───────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SaasAdmin_CallsNextAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // SaasAdmin has no tenant_id claim
        var httpCtx = BuildAuthenticatedContext(tenantId: null, role: "SaasAdmin");
        await middleware.InvokeAsync(httpCtx, ctx);

        Assert.True(nextCalled);
    }

    // ── anonymous request passes through ────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_Unauthenticated_CallsNextAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var nextCalled = false;
        var middleware = new TenantStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(BuildUnauthenticatedContext(), ctx);

        Assert.True(nextCalled);
    }

    // ── response body contains error code ───────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SuspendedTenant_ResponseBodyContainsTenantSuspendedAsync()
    {
        await using var ctx = await db.CreateContextAsync();
        var tenant = Tenant.Create("Suspended Body Test MW");
        tenant.Suspend();
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        var middleware = new TenantStatusMiddleware(_ => Task.CompletedTask);
        var httpCtx = BuildAuthenticatedContext(tenant.Id);
        httpCtx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpCtx, ctx);

        httpCtx.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpCtx.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("tenant_suspended", body, StringComparison.Ordinal);
    }
}
