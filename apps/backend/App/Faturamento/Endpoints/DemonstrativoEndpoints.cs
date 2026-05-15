namespace App.Faturamento.Endpoints;

internal static class DemonstrativoEndpoints
{
    internal static void MapDemonstrativoEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/demonstrativos").RequireAuthorization("TenantAccess");

        g.MapGet("", ListarAsync);
        g.MapGet("{id:guid}", ObterPorIdAsync);
        g.MapPost("", CriarAsync);
        g.MapPut("{id:guid}", AtualizarAsync);
        g.MapDelete("{id:guid}", ExcluirAsync);

        g.MapPost("{id:guid}/itens", AdicionarItemAsync);
        g.MapDelete("{id:guid}/itens/{itemId:guid}", RemoverItemAsync);

        g.MapPost("{id:guid}/itens/{itemId:guid}/conciliar", ConciliarItemAsync);
        g.MapDelete("{id:guid}/itens/{itemId:guid}/conciliar", DesconciliarItemAsync);
    }

    private static async Task<IResult> ListarAsync(
        [AsParameters] ListarDemonstrativosRequest req,
        DemonstrativoService service, CancellationToken ct)
    {
        var query = new ListarDemonstrativosQuery(
            req.OperadoraId, req.Competencia, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterPorIdAsync(
        Guid id, DemonstrativoService service, CancellationToken ct)
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

    private static async Task<IResult> CriarAsync(
        CriarDemonstrativoRequest body, DemonstrativoService service, CancellationToken ct)
    {
        var cmd = new CriarDemonstrativoCommand(
            body.OperadoraId, body.Competencia, body.DataRecebimento, body.Observacao);

        var result = await service.CriarAsync(cmd, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Created(
            $"/api/v1/admin/demonstrativos/{result.Value!.Header.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarAsync(
        Guid id, AtualizarDemonstrativoRequest body, DemonstrativoService service, CancellationToken ct)
    {
        var cmd = new AtualizarDemonstrativoCommand(
            body.OperadoraId, body.Competencia, body.DataRecebimento, body.Observacao);

        var result = await service.AtualizarAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirAsync(
        Guid id, DemonstrativoService service, CancellationToken ct)
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

    private static async Task<IResult> AdicionarItemAsync(
        Guid id, AdicionarItemRequest body, DemonstrativoService service, CancellationToken ct)
    {
        var cmd = new AdicionarItemCommand(
            body.Senha, body.CodigoTuss, body.Descricao,
            body.ValorApresentado, body.ValorPago, body.MotivoGlosa);

        var result = await service.AdicionarItemAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/demonstrativos/{id}", result.Value);
    }

    private static async Task<IResult> RemoverItemAsync(
        Guid id, Guid itemId, DemonstrativoService service, CancellationToken ct)
    {
        var result = await service.RemoverItemAsync(id, itemId, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ConciliarItemAsync(
        Guid id, Guid itemId, ConciliarItemRequest body, DemonstrativoService service, CancellationToken ct)
    {
        var result = await service.ConciliarItemAsync(id, itemId, new ConciliarItemCommand(body.ItemGuiaId), ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DesconciliarItemAsync(
        Guid id, Guid itemId, DemonstrativoService service, CancellationToken ct)
    {
        var result = await service.DesconciliarItemAsync(id, itemId, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.NoContent();
    }
}

internal sealed record ListarDemonstrativosRequest(
    Guid? OperadoraId = null,
    string? Competencia = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record CriarDemonstrativoRequest(
    Guid OperadoraId,
    string Competencia,
    DateOnly DataRecebimento,
    string? Observacao);

internal sealed record AtualizarDemonstrativoRequest(
    Guid OperadoraId,
    string Competencia,
    DateOnly DataRecebimento,
    string? Observacao);

internal sealed record AdicionarItemRequest(
    string Senha,
    string CodigoTuss,
    string? Descricao,
    decimal ValorApresentado,
    decimal ValorPago,
    string? MotivoGlosa);

internal sealed record ConciliarItemRequest(Guid ItemGuiaId);
