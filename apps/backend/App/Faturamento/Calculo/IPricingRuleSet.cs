namespace App.Faturamento.Motor;

internal interface IPricingRuleSet
{
    Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default);
}
