using App.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace App.Faturamento.Endpoints;

internal static class GuiaEndpoints
{
    internal static void MapGuiaEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin/guias").RequireAuthorization("TenantAccess");

        g.MapGet("", ListarGuiasAsync);
        g.MapGet("{id:guid}", ObterGuiaPorIdAsync);
        g.MapGet("{id:guid}/calculo", ObterCalculoAsync);
        g.MapPost("", CriarGuiaAsync);
        g.MapPost("{id:guid}/recalcular", RecalcularGuiaAsync);
        g.MapPut("{id:guid}", AtualizarGuiaAsync);
        g.MapPatch("{id:guid}/observacao", AtualizarObservacaoAsync);
        g.MapPatch("{id:guid}/itens/{itemId:guid}/valor-apurado", AtualizarValorApuradoItemAsync);
        g.MapPost("importar-csv", ImportarCsvAsync).DisableAntiforgery();
        g.MapPatch("{id:guid}/itens/{itemId:guid}/pagamento", AtualizarPagamentoItemAsync);
        g.MapDelete("{id:guid}", ExcluirGuiaAsync);
    }

    private static async Task<IResult> ListarGuiasAsync(
        [AsParameters] ListarGuiasRequest req,
        GuiaService service, CancellationToken ct)
    {
        var query = new ListarGuiasQuery(
            req.PrestadorId, req.OperadoraId,
            req.DataInicio, req.DataFim,
            req.Situacao, req.NumeroGuia, req.Beneficiario,
            req.SemRecurso, req.SomenteComGlosa,
            req.Pagina, req.ItensPorPagina,
            req.OrdenarPor, req.Descendente);
        var result = await service.ListarAsync(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ObterGuiaPorIdAsync(
        Guid id, GuiaService service, CancellationToken ct)
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

    private static async Task<IResult> ObterCalculoAsync(
        Guid id, GuiaService service, CancellationToken ct)
    {
        var result = await service.ObterCalculoAsync(id, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> RecalcularGuiaAsync(
        Guid id, GuiaService service, CancellationToken ct)
    {
        var result = await service.RecalcularAsync(id, ct);
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

    private static async Task<IResult> CriarGuiaAsync(
        CriarGuiaRequest body, GuiaService service, CancellationToken ct)
    {
        var cmd = new CriarGuiaCommand(
            body.PrestadorId, body.OperadoraId, body.BeneficiarioId,
            body.NumeroGuia, body.DataAtendimento, body.EhPacote, body.Observacao,
            body.Itens.Select(i => new CriarItemGuiaCommand(
                i.ProcedimentoId, i.PosicaoExecutor, i.PercentualOrdem,
                i.ViaAcesso, i.Acomodacao, i.EhUrgencia, i.ValorApurado, i.TempoAnestesicoMin)).ToList());

        var result = await service.CriarAsync(cmd, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Created($"/api/v1/admin/guias/{result.Value!.Id}", result.Value);
    }

    private static async Task<IResult> AtualizarGuiaAsync(
        Guid id, AtualizarGuiaRequest body, GuiaService service, CancellationToken ct)
    {
        var cmd = new AtualizarGuiaCommand(
            body.OperadoraId, body.BeneficiarioId,
            body.NumeroGuia, body.DataAtendimento, body.EhPacote, body.Observacao,
            body.Itens.Select(i => new CriarItemGuiaCommand(
                i.ProcedimentoId, i.PosicaoExecutor, i.PercentualOrdem,
                i.ViaAcesso, i.Acomodacao, i.EhUrgencia, i.ValorApurado, i.TempoAnestesicoMin)).ToList());

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

    private static async Task<IResult> AtualizarObservacaoAsync(
        Guid id, AtualizarObservacaoRequest req, GuiaService service, CancellationToken ct)
    {
        var result = await service.AtualizarObservacaoAsync(id, new(req.Observacao), ct);
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

    private static async Task<IResult> AtualizarValorApuradoItemAsync(
        Guid id, Guid itemId, AtualizarValorApuradoItemRequest req,
        GuiaService service, CancellationToken ct)
    {
        var result = await service.AtualizarValorApuradoItemAsync(id, itemId, new(req.ValorApurado), ct);
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

    private static async Task<IResult> ImportarCsvAsync(
        IFormFile arquivo,
        [FromForm] Guid prestadorId,
        [FromForm] Guid operadoraId,
        [FromForm] bool somenteValidar,
        ImportacaoGuiaCsvService service,
        CancellationToken ct)
    {
        await using var stream = arquivo.OpenReadStream();
        var result = await service.ImportarAsync(stream, prestadorId, operadoraId, somenteValidar, ct);

        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ValidationError => StatusCodes.Status400BadRequest,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> AtualizarPagamentoItemAsync(
        Guid id, Guid itemId, AtualizarPagamentoItemRequest req,
        GuiaService service, CancellationToken ct)
    {
        var result = await service.AtualizarPagamentoItemAsync(id, itemId, req.ValorLiquidado, req.MotivoGlosa, ct);
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

    private static async Task<IResult> ExcluirGuiaAsync(
        Guid id, GuiaService service, CancellationToken ct)
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

internal sealed record ListarGuiasRequest(
    Guid? PrestadorId = null, Guid? OperadoraId = null,
    DateOnly? DataInicio = null, DateOnly? DataFim = null,
    SituacaoGuia? Situacao = null, string? NumeroGuia = null, string? Beneficiario = null,
    bool? SemRecurso = null, bool? SomenteComGlosa = null,
    int Pagina = 1, int ItensPorPagina = 20,
    GuiaOrdenacao OrdenarPor = GuiaOrdenacao.DataAtendimento, bool Descendente = true);

internal sealed record CriarItemGuiaRequest(
    Guid ProcedimentoId,
    PosicaoExecutor PosicaoExecutor,
    decimal PercentualOrdem,
    ViaAcesso ViaAcesso,
    Acomodacao Acomodacao,
    bool EhUrgencia,
    decimal? ValorApurado,
    int? TempoAnestesicoMin = null);

internal sealed record CriarGuiaRequest(
    Guid PrestadorId,
    Guid OperadoraId,
    Guid? BeneficiarioId,
    string NumeroGuia,
    DateOnly DataAtendimento,
    bool EhPacote,
    string Observacao,
    IReadOnlyList<CriarItemGuiaRequest> Itens);

internal sealed record AtualizarObservacaoRequest(string Observacao);

internal sealed record AtualizarValorApuradoItemRequest(decimal? ValorApurado);

internal sealed record AtualizarPagamentoItemRequest(decimal? ValorLiquidado, string? MotivoGlosa);

internal sealed record AtualizarGuiaRequest(
    Guid OperadoraId,
    Guid? BeneficiarioId,
    string NumeroGuia,
    DateOnly DataAtendimento,
    bool EhPacote,
    string Observacao,
    IReadOnlyList<CriarItemGuiaRequest> Itens);
