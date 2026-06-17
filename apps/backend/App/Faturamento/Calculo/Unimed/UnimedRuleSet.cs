using App.Catalog;
using App.Data;
using App.Faturamento.Motor.Unimed.Modifiers;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento.Motor.Unimed;

internal sealed class UnimedRuleSet(AppDbContext db) : IPricingRuleSet
{
    private static readonly decimal[] _cascata = [1.0m, 0.5m, 0.4m, 0.3m, 0.2m, 0.1m, 0.1m];

    private static decimal CascataFator(int rank) => rank < _cascata.Length ? _cascata[rank] : 0.1m;

    private sealed record PreparedItem(
        ApurarItemInput Item,
        decimal? ValorBase,
        ApuracaoItemResult? EarlyExit,
        Procedimento? Proc);

    public async Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default)
    {
        // Fase 1: resolver valor base de cada item (sem aplicar cascata ainda)
        var prepared = new List<PreparedItem>(ctx.Itens.Count);
        foreach (var item in ctx.Itens)
        {
            prepared.Add(await PrepararItemAsync(ctx, item, ct));
        }

        // Fase 2: agrupar por PosicaoExecutor, ordenar por valor base desc, atribuir fator da cascata
        var fatores = new Dictionary<Guid, decimal>();
        foreach (var grupo in prepared.Where(p => p.EarlyExit is null).GroupBy(p => p.Item.Posicao))
        {
            var ordenados = grupo
                .OrderByDescending(p => p.ValorBase!.Value)
                .ThenBy(p => p.Item.ProcedimentoId)
                .ThenBy(p => p.Item.ItemGuiaId)
                .ToList();
            for (var rank = 0; rank < ordenados.Count; rank++)
            {
                fatores[ordenados[rank].Item.ItemGuiaId] = CascataFator(rank);
            }
        }

        // Fase 3: aplicar pipeline com o fator derivado
        var resultados = new List<ApuracaoItemResult>(ctx.Itens.Count);
        foreach (var p in prepared)
        {
            if (p.EarlyExit is not null)
            {
                resultados.Add(p.EarlyExit);
                continue;
            }

            var fator = fatores[p.Item.ItemGuiaId];
            resultados.Add(p.Item.Posicao == PosicaoExecutor.Anestesista
                ? ApurarAnestesista(p.Item, p.ValorBase!.Value, p.Proc!, fator)
                : ApurarCirurgiao(p.Item, p.ValorBase!.Value, p.Proc, fator));
        }

        return resultados;
    }

    private async Task<PreparedItem> PrepararItemAsync(
        ApurarGuiaContext ctx, ApurarItemInput item, CancellationToken ct)
    {
        if (item.Posicao == PosicaoExecutor.Anestesista)
        {
            var proc = await db.Procedimentos
                .Where(p => p.Id == item.ProcedimentoId)
                .FirstOrDefaultAsync(ct);

            if (proc?.PorteAnestesico is null)
            {
                return new PreparedItem(item, null,
                    new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Indeterminado, null, 1.0m, []), proc);
            }

            var tabelaPorte = await db.TabelasPorteAnestesico
                .Where(t => t.OperadoraId == ctx.OperadoraId && t.PorteLetra == proc.PorteAnestesico)
                .FirstOrDefaultAsync(ct);

            if (tabelaPorte is null)
            {
                return new PreparedItem(item, null,
                    new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemTabela, null, 1.0m, []), proc);
            }

            var valorRef = item.Acomodacao switch
            {
                Acomodacao.Apartamento => tabelaPorte.ValorApartamento,
                Acomodacao.Ambulatorial => tabelaPorte.ValorAmbulatorial ?? tabelaPorte.ValorEnfermaria,
                _ => tabelaPorte.ValorEnfermaria,
            };

            return new PreparedItem(item, valorRef, null, proc);
        }
        else
        {
            var tabela = await db.TabelasProcedimento
                .Where(t => t.OperadoraId == ctx.OperadoraId && t.ProcedimentoId == item.ProcedimentoId)
                .FirstOrDefaultAsync(ct);

            if (tabela is null)
            {
                return new PreparedItem(item, null,
                    new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemTabela, null, 1.0m, []), null);
            }

            var proc = await db.Procedimentos
                .Where(p => p.Id == item.ProcedimentoId)
                .FirstOrDefaultAsync(ct);

            return new PreparedItem(item, tabela.Valor, null, proc);
        }
    }

    private static ApuracaoItemResult ApurarCirurgiao(
        ApurarItemInput item, decimal valorBase, Procedimento? proc, decimal fator)
    {
        var passos = new List<PassoApuracao> { new("ValorBase", 1.0m, valorBase) };
        var valorAtual = valorBase;
        valorAtual = AplicarModifier(OrdemProcedimentoModifier.Aplicar(fator, valorAtual), passos);
        valorAtual = AplicarModifier(VideolaparoscopiaModifier.Aplicar(item.Via, proc?.TemPorteProprioVideo ?? false, valorAtual), passos);
        valorAtual = AplicarModifier(AcomodacaoModifier.Aplicar(item.Acomodacao, item.Posicao, valorAtual), passos);
        valorAtual = AplicarModifier(UrgenciaModifier.Aplicar(item.EhUrgencia, proc?.EhSadt ?? false, valorAtual), passos);
        AplicarModifier(PosicaoExecutorModifier.Aplicar(item.Posicao, valorAtual), passos);
        return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, passos[^1].ValorResultante, fator, passos);
    }

    private static ApuracaoItemResult ApurarAnestesista(
        ApurarItemInput item, decimal valorBase, Procedimento proc, decimal fator)
    {
        var (valorFinal, passos) = AnestesiaCalculator.Calcular(valorBase, fator, item.EhUrgencia, proc.EhSadt);
        return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, valorFinal, fator, passos);
    }

    private static decimal AplicarModifier(PassoApuracao passo, List<PassoApuracao> passos)
    {
        if (passo.Fator != 1.0m)
        {
            passos.Add(passo);
        }

        return passo.ValorResultante;
    }
}
