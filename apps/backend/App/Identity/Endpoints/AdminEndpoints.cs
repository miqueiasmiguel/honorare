namespace App.Identity.Endpoints;

internal static class AdminEndpoints
{
    internal static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").RequireAuthorization("TenantAccess");

        g.MapGet("/users", GetUsersAsync);
        g.MapPatch("/users/{userId}/status", UpdateUserStatusAsync);
        g.MapGet("/profile", GetProfileAsync);
        g.MapPatch("/profile", UpdateProfileAsync);
    }

    private static async Task<IResult> GetUsersAsync(
        AdminService adminService, CancellationToken ct)
    {
        var users = await adminService.GetUsersAsync(ct);
        return Results.Ok(users);
    }

    private static async Task<IResult> UpdateUserStatusAsync(
        Guid userId, UpdateAdminUserStatusRequest body,
        AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.UpdateUserStatusAsync(userId, body.IsActive, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ForbiddenError => StatusCodes.Status403Forbidden,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetProfileAsync(
        AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.GetProfileAsync(ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest body, AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.UpdateProfileAsync(body.Nome, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }
}

internal sealed record UpdateAdminUserStatusRequest(bool IsActive);

internal sealed record UpdateProfileRequest(string Nome);
