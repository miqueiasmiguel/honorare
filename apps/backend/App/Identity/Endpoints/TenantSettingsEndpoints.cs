namespace App.Identity.Endpoints;

internal static class TenantSettingsEndpoints
{
    internal static void MapTenantSettingsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/tenant").RequireAuthorization("TenantAccess");
        g.MapGet("/", GetSettingsAsync);
        g.MapPatch("/", RenameAsync);
    }

    private static async Task<IResult> GetSettingsAsync(
        TenantSettingsService service, CancellationToken ct)
    {
        var result = await service.GetSettingsAsync(ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> RenameAsync(
        RenameTenantRequest body, TenantSettingsService service, CancellationToken ct)
    {
        var result = await service.RenameAsync(body.Name, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }
}

internal sealed record RenameTenantRequest(string Name);
