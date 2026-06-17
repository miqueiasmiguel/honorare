using App.Catalog;
using App.Faturamento;
using App.Faturamento.Motor;

namespace Faturamento.Tests.Motor;

public sealed class NullRuleSetTests
{
    private static ApurarItemInput CriarItem() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PosicaoExecutor.Cirurgiao,
            ViaAcesso.Convencional,
            Acomodacao.Enfermaria,
            false);

    private static ApurarGuiaContext CriarContexto(int qtdItens)
    {
        var itens = Enumerable.Range(0, qtdItens).Select(_ => CriarItem()).ToList();
        return new ApurarGuiaContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), itens);
    }

    [Fact]
    public async Task ApurarAsync_RetornaTodosOsItensComoIndeterminadoAsync()
    {
        var sut = new NullRuleSet();
        var ctx = CriarContexto(3);

        var resultado = await sut.ApurarAsync(ctx);

        Assert.All(resultado, r => Assert.Equal(SituacaoApuracao.Indeterminado, r.Situacao));
    }

    [Fact]
    public async Task ApurarAsync_ValorApuradoSempreNuloAsync()
    {
        var sut = new NullRuleSet();
        var ctx = CriarContexto(3);

        var resultado = await sut.ApurarAsync(ctx);

        Assert.All(resultado, r => Assert.Null(r.ValorApurado));
    }

    [Fact]
    public async Task ApurarAsync_PassosSempreVaziosAsync()
    {
        var sut = new NullRuleSet();
        var ctx = CriarContexto(3);

        var resultado = await sut.ApurarAsync(ctx);

        Assert.All(resultado, r => Assert.Empty(r.Passos));
    }

    [Fact]
    public async Task ApurarAsync_RetornaUmResultadoPorItemAsync()
    {
        var sut = new NullRuleSet();
        var ctx = CriarContexto(5);

        var resultado = await sut.ApurarAsync(ctx);

        Assert.Equal(5, resultado.Count);
    }
}
