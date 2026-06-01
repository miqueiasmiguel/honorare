using App.Catalog;
using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento.Endpoints;

internal static class MedicoEndpoints
{
    internal static void MapMedicoEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/medico/guias").RequireAuthorization("MedicoAccess");
        g.MapGet("", ListarGuiasAsync);
        g.MapGet("{id:guid}", ObterGuiaPorIdAsync);
    }

    private static async Task<IResult> ListarGuiasAsync(
        [AsParameters] MedicoListarGuiasRequest req,
        AppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var medicoId = currentUser.MedicoId;
        if (medicoId is null)
        {
            return Results.Forbid();
        }

        var q = from g in db.Guias
                join op in db.Operadoras on g.OperadoraId equals op.Id
                join b in db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
                from b in bs.DefaultIfEmpty()
                where g.PrestadorId == medicoId.Value && g.Situacao != SituacaoGuia.Liquidada
                select new
                {
                    g.Id,
                    g.OperadoraId,
                    OperadoraNome = op.Nome,
                    BeneficiarioNome = (string?)b.Nome,
                    BeneficiarioCarteira = (string?)b.Carteira,
                    g.NumeroGuia,
                    g.DataAtendimento,
                    g.Situacao,
                    g.Observacao,
                    g.CriadoEm,
                };

        if (req.OperadoraId.HasValue)
        {
            q = q.Where(x => x.OperadoraId == req.OperadoraId.Value);
        }

        if (req.DataInicio.HasValue)
        {
            q = q.Where(x => x.DataAtendimento >= req.DataInicio.Value);
        }

        if (req.DataFim.HasValue)
        {
            q = q.Where(x => x.DataAtendimento <= req.DataFim.Value);
        }

        var total = await q.CountAsync(ct);
        var itensPorPagina = Math.Min(req.ItensPorPagina, 100);
        var skip = (req.Pagina - 1) * itensPorPagina;

        var pagina = await q
            .OrderByDescending(x => x.DataAtendimento)
            .ThenByDescending(x => x.CriadoEm)
            .Skip(skip)
            .Take(itensPorPagina)
            .ToListAsync(ct);

        var ids = pagina.Select(x => x.Id).ToList();
        var counts = await db.ItensGuia
            .Where(i => ids.Contains(i.GuiaId))
            .GroupBy(i => i.GuiaId)
            .ToDictionaryAsync(grp => grp.Key, grp => grp.Count(), ct);

        var itens = pagina.Select(x => new MedicoGuiaSummaryDto(
            x.Id,
            x.OperadoraNome,
            x.BeneficiarioNome,
            x.BeneficiarioCarteira,
            x.NumeroGuia,
            x.DataAtendimento,
            x.Situacao,
            counts.GetValueOrDefault(x.Id, 0),
            !string.IsNullOrEmpty(x.Observacao))).ToList();

        return Results.Ok(new MedicoListarGuiasResult(itens, total, req.Pagina, req.ItensPorPagina));
    }

    private static async Task<IResult> ObterGuiaPorIdAsync(
        Guid id, AppDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var medicoId = currentUser.MedicoId;
        if (medicoId is null)
        {
            return Results.Forbid();
        }

        var header = await (from g in db.Guias
                            join op in db.Operadoras on g.OperadoraId equals op.Id
                            join b in db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
                            from b in bs.DefaultIfEmpty()
                            where g.Id == id && g.PrestadorId == medicoId.Value
                            select new
                            {
                                g.Id,
                                OperadoraNome = op.Nome,
                                BeneficiarioNome = (string?)b.Nome,
                                BeneficiarioCarteira = (string?)b.Carteira,
                                g.NumeroGuia,
                                g.DataAtendimento,
                                g.Situacao,
                                g.EhPacote,
                                g.Observacao,
                            })
                           .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return Results.NotFound();
        }

        var itensBase = await (from i in db.ItensGuia
                               join p in db.Procedimentos on i.ProcedimentoId equals p.Id
                               where i.GuiaId == id
                               select new
                               {
                                   i.Id,
                                   p.CodigoTuss,
                                   DescricaoProcedimento = p.Descricao,
                                   i.PosicaoExecutor,
                                   i.ValorApurado,
                                   i.ValorLiquidado,
                               })
                              .ToListAsync(ct);

        var temCalculo = await db.Calculos.AnyAsync(c => c.GuiaId == id, ct);

        var itens = itensBase.Select(i =>
        {
            var situacao = header.EhPacote
                ? "Pacote"
                : !temCalculo
                    ? "NaoCalculado"
                    : i.ValorApurado.HasValue ? "Calculado" : "SemTabela";

            return new MedicoItemGuiaDto(
                i.Id,
                i.CodigoTuss,
                i.DescricaoProcedimento,
                i.PosicaoExecutor,
                situacao,
                i.ValorApurado,
                i.ValorLiquidado);
        }).ToList();

        return Results.Ok(new MedicoGuiaDetalheDto(
            header.Id,
            header.OperadoraNome,
            header.BeneficiarioNome,
            header.BeneficiarioCarteira,
            header.NumeroGuia,
            header.DataAtendimento,
            header.Situacao,
            header.EhPacote,
            header.Observacao,
            itens));
    }
}

internal sealed record MedicoListarGuiasRequest(
    Guid? OperadoraId = null,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null,
    int Pagina = 1,
    int ItensPorPagina = 20);

internal sealed record MedicoGuiaSummaryDto(
    Guid Id,
    string OperadoraNome,
    string? BeneficiarioNome,
    string? BeneficiarioCarteira,
    string NumeroGuia,
    DateOnly DataAtendimento,
    SituacaoGuia Situacao,
    int TotalItens,
    bool TemObservacao);

internal sealed record MedicoListarGuiasResult(
    IReadOnlyList<MedicoGuiaSummaryDto> Itens,
    int Total,
    int Pagina,
    int ItensPorPagina);

internal sealed record MedicoItemGuiaDto(
    Guid Id,
    string CodigoTuss,
    string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor,
    string SituacaoCalculo,
    decimal? ValorApurado,
    decimal? ValorLiquidado);

internal sealed record MedicoGuiaDetalheDto(
    Guid Id,
    string OperadoraNome,
    string? BeneficiarioNome,
    string? BeneficiarioCarteira,
    string NumeroGuia,
    DateOnly DataAtendimento,
    SituacaoGuia Situacao,
    bool EhPacote,
    string Observacao,
    IReadOnlyList<MedicoItemGuiaDto> Itens);
