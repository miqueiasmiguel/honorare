using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class UrgenciaModifierTests
{
    [Fact]
    public void SemUrgencia_FatorNeutro()
    {
        var passo = UrgenciaModifier.Aplicar(false, false, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("Urgencia", passo.Regra);
    }

    [Fact]
    public void Urgencia_NaoSadt_Fator1_3()
    {
        var passo = UrgenciaModifier.Aplicar(true, false, 100m);

        Assert.Equal(1.3m, passo.Fator);
        Assert.Equal(130m, passo.ValorResultante);
    }

    [Fact]
    public void Urgencia_EhSadt_FatorNeutro()
    {
        var passo = UrgenciaModifier.Aplicar(true, true, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }
}
