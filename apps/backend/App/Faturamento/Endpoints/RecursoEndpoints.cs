using App.Faturamento.Pdf;

namespace App.Faturamento.Endpoints;

internal static class RecursoEndpoints
{
    internal static void MapRecursoEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/recursos").RequireAuthorization("TenantAccess");

        g.MapGet("", ListarRecursosAsync);
        g.MapGet("{id:guid}", ObterRecursoPorIdAsync);
        g.MapPost("", CriarRecursoAsync);
        g.MapPut("{id:guid}", AtualizarRecursoAsync);
        g.MapDelete("{id:guid}", ExcluirRecursoAsync);
        g.MapPost("{id:guid}/guias/lote", AdicionarGuiasEmLoteAsync);
        g.MapPost("{id:guid}/guias/{guiaId:guid}", AdicionarGuiaAsync);
        g.MapDelete("{id:guid}/guias/{guiaId:guid}", RemoverGuiaAsync);
        g.MapGet("{id:guid}/pdf", GerarPdfAsync);
    }

    private static async Task<IResult> ListarRecursosAsync(
        [AsParameters] ListarRecursosRequest req,
        RecursoService service, CancellationToken ct)
    {
        var query = new ListarRecursosQuery(req.OperadoraId, req.PrestadorId, req.Pagina, req.ItensPorPagina);
        var result = await service.ListarAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterRecursoPorIdAsync(
        Guid id, RecursoService service, CancellationToken ct)
    {
        var result = await service.ObterPorIdAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CriarRecursoAsync(
        CriarRecursoRequest body, RecursoService service, CancellationToken ct)
    {
        var cmd = new CriarRecursoCommand(body.OperadoraId, body.PrestadorId, body.DataEmissao, body.Observacao, body.Numero);
        var result = await service.CriarAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ValidationError => StatusCodes.Status422UnprocessableEntity,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/recursos/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarRecursoAsync(
        Guid id, AtualizarRecursoRequest body, RecursoService service, CancellationToken ct)
    {
        var cmd = new AtualizarRecursoCommand(body.OperadoraId, body.PrestadorId, body.DataEmissao, body.Observacao, body.Numero);
        var result = await service.AtualizarAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ValidationError => StatusCodes.Status422UnprocessableEntity,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExcluirRecursoAsync(
        Guid id, RecursoService service, CancellationToken ct)
    {
        var result = await service.ExcluirAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> AdicionarGuiasEmLoteAsync(
        Guid id, AdicionarGuiasEmLoteRequest body, RecursoService service, CancellationToken ct)
    {
        var cmd = new AdicionarGuiasEmLoteCommand(
            body.PrestadorId, body.OperadoraId,
            body.DataInicio, body.DataFim,
            body.Situacao, body.NumeroGuia, body.Beneficiario, body.SomenteComGlosa);
        var result = await service.AdicionarGuiasEmLoteAsync(id, cmd, ct);
        if (result.IsFailure)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error!.Message);
        }

        return Results.Ok(new { adicionadas = result.Value });
    }

    private static async Task<IResult> AdicionarGuiaAsync(
        Guid id, Guid guiaId, RecursoService service, CancellationToken ct)
    {
        await service.AdicionarGuiaAsync(id, guiaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoverGuiaAsync(
        Guid id, Guid guiaId, RecursoService service, CancellationToken ct)
    {
        await service.RemoverGuiaAsync(id, guiaId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GerarPdfAsync(
        Guid id, RecursoService service, CancellationToken ct)
    {
        var dados = await service.ObterDadosPdfAsync(id, ct);
        if (dados.IsFailure)
        {
            return Results.NotFound();
        }

        var doc = new RecursoPdfDocument(dados.Value!);
        var bytes = doc.GeneratePdf();
        var nome = $"RECURSO_{dados.Value!.Numero}_{dados.Value.OperadoraNome}.pdf";
        return Results.File(bytes, "application/pdf", nome);
    }
}

internal sealed record ListarRecursosRequest(
    Guid? OperadoraId = null,
    Guid? PrestadorId = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record CriarRecursoRequest(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao, string Numero);

internal sealed record AtualizarRecursoRequest(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao, string Numero);

internal sealed record AdicionarGuiasEmLoteRequest(
    Guid PrestadorId, Guid OperadoraId,
    DateOnly? DataInicio = null, DateOnly? DataFim = null,
    SituacaoGuia? Situacao = null, string? NumeroGuia = null,
    string? Beneficiario = null, bool? SomenteComGlosa = null);
