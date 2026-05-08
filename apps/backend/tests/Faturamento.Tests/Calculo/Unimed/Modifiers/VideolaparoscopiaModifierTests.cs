using App.Faturamento;
using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class VideolaparoscopiaModifierTests
{
    [Fact]
    public void Convencional_FatorNeutro()
    {
        var passo = VideolaparoscopiaModifier.Aplicar(ViaAcesso.Convencional, false, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("Videolaparoscopia", passo.Regra);
    }

    [Fact]
    public void Videolaparoscopia_SemPorteProprio_Fator1_5()
    {
        var passo = VideolaparoscopiaModifier.Aplicar(ViaAcesso.Videolaparoscopia, false, 100m);

        Assert.Equal(1.5m, passo.Fator);
        Assert.Equal(150m, passo.ValorResultante);
    }

    [Fact]
    public void Videolaparoscopia_ComPorteProprio_FatorNeutro()
    {
        var passo = VideolaparoscopiaModifier.Aplicar(ViaAcesso.Videolaparoscopia, true, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }
}
