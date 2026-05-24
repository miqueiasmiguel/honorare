using App.Catalog;
using App.Data;
using App.Faturamento.Motor.Unimed.Modifiers;
using Microsoft.EntityFrameworkCore;

namespace App.Faturamento.Motor.Unimed;

internal sealed class UnimedRuleSet(AppDbContext db) : IPricingRuleSet
{
    public async Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default)
    {
        var resultados = new List<ApuracaoItemResult>(ctx.Itens.Count);

        foreach (var item in ctx.Itens)
        {
            resultados.Add(await ApurarItemAsync(ctx, item, ct));
        }

        return resultados;
    }

    private async Task<ApuracaoItemResult> ApurarItemAsync(
        ApurarGuiaContext ctx, ApurarItemInput item, CancellationToken ct)
    {
        if (item.Posicao == PosicaoExecutor.Anestesista)
        {
            return await ApurarAnestesistaAsync(ctx, item, ct);
        }

        var tabela = await db.TabelasProcedimento
            .Where(t => t.OperadoraId == ctx.OperadoraId && t.ProcedimentoId == item.ProcedimentoId)
            .FirstOrDefaultAsync(ct);

        if (tabela is null)
        {
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemTabela, null, []);
        }

        var deflator = await db.DeflatoresPrestador
            .Where(d => d.PrestadorId == ctx.PrestadorId && d.OperadoraId == ctx.OperadoraId && d.Posicao == item.Posicao)
            .FirstOrDefaultAsync(ct);

        if (deflator is null)
        {
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemDeflator, null, []);
        }

        var procedimento = await db.Procedimentos
            .Where(p => p.Id == item.ProcedimentoId)
            .FirstOrDefaultAsync(ct);

        var fatorBase = deflator.Percentual / 100m;
        var valorBase = tabela.Valor * fatorBase;
        var passos = new List<PassoApuracao> { new("ValorBase", fatorBase, valorBase) };

        var valorAtual = valorBase;
        valorAtual = AplicarModifier(OrdemProcedimentoModifier.Aplicar(item.Ordem, valorAtual), passos);
        valorAtual = AplicarModifier(VideolaparoscopiaModifier.Aplicar(item.Via, procedimento?.TemPorteProprioVideo ?? false, valorAtual), passos);
        valorAtual = AplicarModifier(AcomodacaoModifier.Aplicar(item.Acomodacao, valorAtual), passos);
        valorAtual = AplicarModifier(UrgenciaModifier.Aplicar(item.EhUrgencia, procedimento?.EhSadt ?? false, valorAtual), passos);
        AplicarModifier(PosicaoExecutorModifier.Aplicar(item.Posicao, valorAtual), passos);

        var valorFinal = passos[^1].ValorResultante;
        return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, valorFinal, passos);
    }

    private async Task<ApuracaoItemResult> ApurarAnestesistaAsync(
        ApurarGuiaContext ctx, ApurarItemInput item, CancellationToken ct)
    {
        var tabela = await db.TabelasProcedimento
            .Where(t => t.OperadoraId == ctx.OperadoraId && t.ProcedimentoId == item.ProcedimentoId)
            .FirstOrDefaultAsync(ct);

        if (tabela is null)
        {
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemTabela, null, []);
        }

        var deflator = await db.DeflatoresPrestador
            .Where(d => d.PrestadorId == ctx.PrestadorId && d.OperadoraId == ctx.OperadoraId
                        && d.Posicao == PosicaoExecutor.Anestesista)
            .FirstOrDefaultAsync(ct);

        if (deflator is null)
        {
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.SemDeflator, null, []);
        }

        var procedimento = await db.Procedimentos
            .Where(p => p.Id == item.ProcedimentoId)
            .FirstOrDefaultAsync(ct);

        if (procedimento?.PorteAnestesico is null)
        {
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Indeterminado, null, []);
        }

        var (valorFinal, passos) = AnestesiaCalculator.Calcular(
            tabela.Valor, deflator.Percentual,
            item.Ordem, item.EhUrgencia, procedimento.EhSadt);

        return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, valorFinal, passos);
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
