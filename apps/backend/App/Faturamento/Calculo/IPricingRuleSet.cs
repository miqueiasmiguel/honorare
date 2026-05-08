namespace App.Faturamento.Calculo;

internal interface IPricingRuleSet
{
    Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default);
}
