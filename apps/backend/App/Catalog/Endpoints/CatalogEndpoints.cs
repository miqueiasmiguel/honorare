namespace App.Catalog.Endpoints;

internal static class CatalogEndpoints
{
    internal static void MapCatalogEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/operadoras").RequireAuthorization("TenantAccess");

        g.MapGet("", ListarOperadorasAsync);
        g.MapGet("{id:guid}", ObterOperadoraAsync);
        g.MapPost("", CriarOperadoraAsync);
        g.MapPut("{id:guid}", AtualizarOperadoraAsync);
        g.MapDelete("{id:guid}", ExcluirOperadoraAsync);
    }

    private static async Task<IResult> ListarOperadorasAsync(
        [AsParameters] ListarOperadorasRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarOperadorasQuery(req.Nome, req.Ativa, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterOperadoraAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarOperadoraAsync(
        CriarOperadoraRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new CriarOperadoraCommand(body.Nome, body.RegistroAns, body.Cnpj, body.TipoRuleSet);
        var result = await service.CriarAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/operadoras/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarOperadoraAsync(
        Guid id, AtualizarOperadoraRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new AtualizarOperadoraCommand(
            body.Nome, body.RegistroAns, body.Cnpj, body.TipoRuleSet, body.Ativa);
        var result = await service.AtualizarAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirOperadoraAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }
}

internal sealed record ListarOperadorasRequest(
    string? Nome = null,
    bool? Ativa = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record CriarOperadoraRequest(
    string Nome,
    string? RegistroAns,
    string? Cnpj,
    TipoRuleSet TipoRuleSet);

internal sealed record AtualizarOperadoraRequest(
    string Nome,
    string? RegistroAns,
    string? Cnpj,
    TipoRuleSet TipoRuleSet,
    bool Ativa);
