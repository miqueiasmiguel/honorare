using App.Catalog;
using App.Data;
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
            return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Indeterminado, null, []);
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

        var fator = deflator.Percentual / 100m;
        var valorBase = tabela.Valor * fator;
        var passos = new List<PassoApuracao> { new("ValorBase", fator, valorBase) };

        return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, valorBase, passos);
    }
}
