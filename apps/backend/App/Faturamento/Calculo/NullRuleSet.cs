namespace App.Faturamento.Motor;

internal sealed class NullRuleSet : IPricingRuleSet
{
    public Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default)
    {
        IReadOnlyList<ApuracaoItemResult> resultados = ctx.Itens
            .Select(item => new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Indeterminado, null, []))
            .ToList();

        return Task.FromResult(resultados);
    }
}
