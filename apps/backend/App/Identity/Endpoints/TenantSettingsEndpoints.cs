namespace App.Identity.Endpoints;

internal static class TenantSettingsEndpoints
{
    internal static void MapTenantSettingsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/tenant").RequireAuthorization("TenantAccess");
        g.MapGet("/", GetSettingsAsync);
        g.MapPatch("/", RenameAsync);
        g.MapPut("/codigos-nao-recorriveis", AtualizarCodigosNaoRecorriveisAsync);
        g.MapPost("/logo", UploadLogoAsync).DisableAntiforgery();
        g.MapGet("/logo", GetLogoAsync);
        g.MapDelete("/logo", DeleteLogoAsync);
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

    private static async Task<IResult> UploadLogoAsync(
        IFormFile file, TenantSettingsService svc, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await svc.UploadLogoAsync(bytes, ct);
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

    private static async Task<IResult> GetLogoAsync(
        TenantSettingsService svc, CancellationToken ct)
    {
        var result = await svc.GetLogoAsync(ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.File(result.Value!.Content, result.Value.ContentType);
    }

    private static async Task<IResult> DeleteLogoAsync(
        TenantSettingsService svc, CancellationToken ct)
    {
        var result = await svc.DeleteLogoAsync(ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> AtualizarCodigosNaoRecorriveisAsync(
        AtualizarCodigosNaoRecorriveisRequest body, TenantSettingsService svc, CancellationToken ct)
    {
        var result = await svc.AtualizarCodigosNaoRecorriveisAsync(body.Codigos, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                ValidationError => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }
}

internal sealed record RenameTenantRequest(string Name);
internal sealed record AtualizarCodigosNaoRecorriveisRequest(IReadOnlyList<string> Codigos);
