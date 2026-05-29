using App.Catalog;

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
        g.MapPut("{id:guid}", AtualizarGuiaAsync);
        g.MapDelete("{id:guid}", ExcluirGuiaAsync);
    }

    private static async Task<IResult> ListarGuiasAsync(
        [AsParameters] ListarGuiasRequest req,
        GuiaService service, CancellationToken ct)
    {
        var query = new ListarGuiasQuery(
            req.PrestadorId, req.DataInicio, req.DataFim,
            req.Situacao, req.Pagina, req.ItensPorPagina);
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

    private static async Task<IResult> CriarGuiaAsync(
        CriarGuiaRequest body, GuiaService service, CancellationToken ct)
    {
        var cmd = new CriarGuiaCommand(
            body.PrestadorId, body.OperadoraId, body.BeneficiarioId,
            body.Senha, body.DataAtendimento, body.EhPacote, body.Observacao,
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
            body.Senha, body.DataAtendimento, body.EhPacote, body.Observacao,
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
    Guid? PrestadorId = null,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null,
    SituacaoGuia? Situacao = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

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
    string Senha,
    DateOnly DataAtendimento,
    bool EhPacote,
    string Observacao,
    IReadOnlyList<CriarItemGuiaRequest> Itens);

internal sealed record AtualizarGuiaRequest(
    Guid OperadoraId,
    Guid? BeneficiarioId,
    string Senha,
    DateOnly DataAtendimento,
    bool EhPacote,
    string Observacao,
    IReadOnlyList<CriarItemGuiaRequest> Itens);
