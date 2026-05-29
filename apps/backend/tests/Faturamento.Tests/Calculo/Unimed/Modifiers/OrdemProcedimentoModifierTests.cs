using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class OrdemProcedimentoModifierTests
{
    [Fact]
    public void Percentual1_0_RetornaFator1_0()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(1.0m, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("OrdemProcedimento", passo.Regra);
    }

    [Fact]
    public void Percentual0_7_RetornaFator0_7()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(0.7m, 100m);

        Assert.Equal(0.7m, passo.Fator);
        Assert.Equal(70m, passo.ValorResultante);
    }

    [Fact]
    public void Percentual0_5_RetornaFator0_5()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(0.5m, 100m);

        Assert.Equal(0.5m, passo.Fator);
        Assert.Equal(50m, passo.ValorResultante);
    }

    [Fact]
    public void Percentual0_4_RetornaFator0_4()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(0.4m, 100m);

        Assert.Equal(0.4m, passo.Fator);
        Assert.Equal(40m, passo.ValorResultante);
    }

    [Fact]
    public void Percentual0_3_RetornaFator0_3()
    {
        var passo = OrdemProcedimentoModifier.Aplicar(0.3m, 100m);

        Assert.Equal(0.3m, passo.Fator);
        Assert.Equal(30m, passo.ValorResultante);
    }
}
