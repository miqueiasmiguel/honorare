namespace App.Identity.Endpoints;

internal static class SaasEndpoints
{
    internal static void MapSaasEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/saas").RequireAuthorization("SaasOnly");

        g.MapGet("/tenants", ListTenantsAsync);
        g.MapPost("/tenants", CreateTenantAsync);
        g.MapPatch("/tenants/{tenantId}/status", UpdateTenantStatusAsync);
        g.MapGet("/tenants/{tenantId}/users", ListTenantUsersAsync);
        g.MapPost("/tenants/{tenantId}/users", CreateUserAsync);
        g.MapPatch("/tenants/{tenantId}/users/{userId}/status", UpdateUserStatusAsync);
    }

    private static async Task<IResult> ListTenantsAsync(
        SaasService saasService, CancellationToken ct)
    {
        var tenants = await saasService.ListTenantsAsync(ct);
        return Results.Ok(tenants);
    }

    private static async Task<IResult> CreateTenantAsync(
        CreateTenantRequest body, SaasService saasService, CancellationToken ct)
    {
        var result = await saasService.CreateTenantAsync(body.TenantName, body.OwnerEmail, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error is ConflictError
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/saas/tenants/{result.Value!.TenantId}", result.Value);
    }

    private static async Task<IResult> UpdateTenantStatusAsync(
        Guid tenantId, UpdateTenantStatusRequest body, SaasService saasService, CancellationToken ct)
    {
        var result = await saasService.UpdateTenantStatusAsync(tenantId, body.Status, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error is NotFoundError
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ListTenantUsersAsync(
        Guid tenantId, SaasService saasService, CancellationToken ct)
    {
        var result = await saasService.ListTenantUsersAsync(tenantId, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateUserAsync(
        Guid tenantId, CreateUserRequest body, SaasService saasService, CancellationToken ct)
    {
        var result = await saasService.CreateUserAsync(tenantId, body.Email, body.Role, body.MedicoId, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created(
            $"/api/v1/saas/tenants/{tenantId}/users/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> UpdateUserStatusAsync(
        Guid tenantId, Guid userId, UpdateUserStatusRequest body,
        SaasService saasService, CancellationToken ct)
    {
        var result = await saasService.UpdateUserStatusAsync(tenantId, userId, body.IsActive, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }
}

internal sealed record CreateTenantRequest(string TenantName, string OwnerEmail);

internal sealed record UpdateTenantStatusRequest(string Status);

internal sealed record CreateUserRequest(string Email, string Role, Guid? MedicoId);

internal sealed record UpdateUserStatusRequest(bool IsActive);
