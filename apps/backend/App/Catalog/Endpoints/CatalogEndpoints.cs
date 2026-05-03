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

        var gp = app.MapGroup("/api/v1/admin/procedimentos").RequireAuthorization("TenantAccess");

        gp.MapGet("", ListarProcedimentosAsync);
        gp.MapGet("{id:guid}", ObterProcedimentoAsync);
        gp.MapPost("", CriarProcedimentoAsync);
        gp.MapPut("{id:guid}", AtualizarProcedimentoAsync);
        gp.MapDelete("{id:guid}", ExcluirProcedimentoAsync);
        gp.MapPost("importar-csv", ImportarCsvAsync).DisableAntiforgery();
    }

    // ── Operadora handlers ────────────────────────────────────────────────────

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

    // ── Procedimento handlers ─────────────────────────────────────────────────

    private static async Task<IResult> ListarProcedimentosAsync(
        [AsParameters] ListarProcedimentosRequest req,
        CatalogService service, CancellationToken ct)
    {
        var query = new ListarProcedimentosQuery(req.Busca, req.Ativo, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarProcedimentosAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterProcedimentoAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ObterProcedimentoPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarProcedimentoAsync(
        SalvarProcedimentoRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarProcedimentoCommand(
            body.CodigoTuss, body.Descricao, body.Porte,
            body.PorteAnestesico, body.EhSadt, body.TemPorteProprioVideo, body.Ativo);
        var result = await service.CriarProcedimentoAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ConflictError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/procedimentos/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarProcedimentoAsync(
        Guid id, SalvarProcedimentoRequest body, CatalogService service, CancellationToken ct)
    {
        var cmd = new SalvarProcedimentoCommand(
            body.CodigoTuss, body.Descricao, body.Porte,
            body.PorteAnestesico, body.EhSadt, body.TemPorteProprioVideo, body.Ativo);
        var result = await service.AtualizarProcedimentoAsync(id, cmd, ct);
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

    private static async Task<IResult> ExcluirProcedimentoAsync(
        Guid id, CatalogService service, CancellationToken ct)
    {
        var result = await service.ExcluirProcedimentoAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ImportarCsvAsync(
        IFormFile? file, CatalogService service, CancellationToken ct)
    {
        if (file is null ||
            !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "É necessário enviar um arquivo com extensão .csv.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "O arquivo excede o tamanho máximo de 5 MB.");
        }

        using var stream = file.OpenReadStream();
        var result = await service.ImportarProcedimentosCsvAsync(stream, ct);
        return Results.Ok(result);
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

internal sealed record ListarProcedimentosRequest(
    string? Busca = null,
    bool? Ativo = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record SalvarProcedimentoRequest(
    string CodigoTuss,
    string Descricao,
    string? Porte,
    int? PorteAnestesico,
    bool EhSadt,
    bool TemPorteProprioVideo,
    bool Ativo);
