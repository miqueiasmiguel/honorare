using App.Faturamento;
using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class AcomodacaoModifierTests
{
    [Fact]
    public void Enfermaria_FatorNeutro()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Enfermaria, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
        Assert.Equal("Acomodacao", passo.Regra);
    }

    [Fact]
    public void Ambulatorial_FatorNeutro()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Ambulatorial, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }

    [Fact]
    public void Apartamento_Fator2_0()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, 100m);

        Assert.Equal(2.0m, passo.Fator);
        Assert.Equal(200m, passo.ValorResultante);
    }
}
