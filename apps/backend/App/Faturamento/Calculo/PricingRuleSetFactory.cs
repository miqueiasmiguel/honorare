using App.Catalog;
using App.Data;
using App.Faturamento.Motor.Unimed;

namespace App.Faturamento.Motor;

internal sealed class PricingRuleSetFactory(AppDbContext db)
{
    internal IPricingRuleSet Criar(TipoRuleSet tipo) =>
        tipo == TipoRuleSet.Unimed ? new UnimedRuleSet(db) : new NullRuleSet();
}
