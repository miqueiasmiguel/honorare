using App.Faturamento;
using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class OrdemProcedimentoModifierTests
{
    [Fact]
    public void Unico_Fator1_0()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(OrdemProcedimento.Unico, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("OrdemProcedimento", passo.Regra);
    }

    [Fact]
    public void Principal_Fator1_0()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(OrdemProcedimento.Principal, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }

    [Fact]
    public void SecundarioMesmaVia_Fator0_5()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(OrdemProcedimento.SecundarioMesmaVia, 100m);

        Assert.Equal(0.5m, passo.Fator);
        Assert.Equal(50m, passo.ValorResultante);
    }

    [Fact]
    public void SecundarioViaDiferente_Fator0_7()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(OrdemProcedimento.SecundarioViaDiferente, 100m);

        Assert.Equal(0.7m, passo.Fator);
        Assert.Equal(70m, passo.ValorResultante);
    }
}
