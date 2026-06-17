using App.Faturamento.Motor;

namespace Faturamento.Tests.Motor;

public sealed class CalculoTypesTests
{
    [Fact]
    public void SituacaoApuracao_PossuiExatamente3Membros()
    {
        var membros = Enum.GetValues<SituacaoApuracao>();

        Assert.Equal(3, membros.Length);
        Assert.Contains(SituacaoApuracao.Calculado, membros);
        Assert.Contains(SituacaoApuracao.SemTabela, membros);
        Assert.Contains(SituacaoApuracao.Indeterminado, membros);
    }

    [Fact]
    public void ApuracaoItemResult_Calculado_ValorApuradoNaoNulo()
    {
        var result = new ApuracaoItemResult(
            Guid.NewGuid(),
            SituacaoApuracao.Calculado,
            100m,
            1.0m,
            []);

        Assert.NotNull(result.ValorApurado);
        Assert.Equal(100m, result.ValorApurado);
    }

    [Fact]
    public void ApuracaoItemResult_SemTabela_ValorApuradoNulo()
    {
        var result = new ApuracaoItemResult(
            Guid.NewGuid(),
            SituacaoApuracao.SemTabela,
            null,
            1.0m,
            []);

        Assert.Null(result.ValorApurado);
    }

    [Fact]
    public void PassoApuracao_ArmazenaPropriedadesCorretamente()
    {
        var passo = new PassoApuracao("ValorBase", 0.8m, 80m);

        Assert.Equal("ValorBase", passo.Regra);
        Assert.Equal(0.8m, passo.Fator);
        Assert.Equal(80m, passo.ValorResultante);
    }
}
