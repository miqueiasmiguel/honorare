using App.Catalog;
using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class PosicaoExecutorModifierTests
{
    [Fact]
    public void Cirurgiao_FatorNeutro()
    {
        var passo = PosicaoExecutorModifier.Aplicar(PosicaoExecutor.Cirurgiao, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("PosicaoExecutor", passo.Regra);
    }

    [Fact]
    public void PrimeiroAuxiliar_Fator0_6()
    {
        var passo = PosicaoExecutorModifier.Aplicar(PosicaoExecutor.PrimeiroAuxiliar, 100m);

        Assert.Equal(0.6m, passo.Fator);
        Assert.Equal(60m, passo.ValorResultante);
    }

    [Fact]
    public void SegundoAuxiliar_Fator0_4()
    {
        var passo = PosicaoExecutorModifier.Aplicar(PosicaoExecutor.SegundoAuxiliar, 100m);

        Assert.Equal(0.4m, passo.Fator);
        Assert.Equal(40m, passo.ValorResultante);
    }

    [Fact]
    public void TerceiroAuxiliar_Fator0_3()
    {
        var passo = PosicaoExecutorModifier.Aplicar(PosicaoExecutor.TerceiroAuxiliar, 100m);

        Assert.Equal(0.3m, passo.Fator);
        Assert.Equal(30m, passo.ValorResultante);
    }

    [Fact]
    public void ClinicoAssistente_FatorNeutro()
    {
        var passo = PosicaoExecutorModifier.Aplicar(PosicaoExecutor.ClinicoAssistente, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }
}
