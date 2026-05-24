using App.Catalog;
using App.Faturamento;
using App.Faturamento.Motor.Unimed.Modifiers;

namespace Faturamento.Tests.Motor.Unimed.Modifiers;

public sealed class AcomodacaoModifierTests
{
    [Fact]
    public void Cirurgiao_Apartamento_Dobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.Cirurgiao, 100m);

        Assert.Equal(2.0m, passo.Fator);
        Assert.Equal(200m, passo.ValorResultante);
    }

    [Fact]
    public void Cirurgiao_Enfermaria_Neutro()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Enfermaria, PosicaoExecutor.Cirurgiao, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }

    [Fact]
    public void Cirurgiao_Ambulatorial_Neutro()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Ambulatorial, PosicaoExecutor.Cirurgiao, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }

    [Fact]
    public void PrimeiroAuxiliar_Apartamento_NaoDobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.PrimeiroAuxiliar, 100m);

        Assert.Equal(1.0m, passo.Fator);
        Assert.Equal(100m, passo.ValorResultante);
    }

    [Fact]
    public void SegundoAuxiliar_Apartamento_NaoDobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.SegundoAuxiliar, 100m);

        Assert.Equal(1.0m, passo.Fator);
    }

    [Fact]
    public void TerceiroAuxiliar_Apartamento_NaoDobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.TerceiroAuxiliar, 100m);

        Assert.Equal(1.0m, passo.Fator);
    }

    [Fact]
    public void ClinicoAssistente_Apartamento_NaoDobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.ClinicoAssistente, 100m);

        Assert.Equal(1.0m, passo.Fator);
    }

    [Fact]
    public void Anestesista_Apartamento_NaoDobra()
    {
        var passo = AcomodacaoModifier.Aplicar(Acomodacao.Apartamento, PosicaoExecutor.Anestesista, 100m);

        Assert.Equal(1.0m, passo.Fator);
    }
}
