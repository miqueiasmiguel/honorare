using App.Catalog;
using App.Faturamento;

namespace Faturamento.Tests.Domain;

public sealed class ItemGuiaTests
{
    private static ItemGuia Novo() => ItemGuia.Create(
        Guid.NewGuid(), Guid.NewGuid(), PosicaoExecutor.Cirurgiao,
        ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);

    [Fact]
    public void Deve_nascer_com_IncluidoNoRecurso_true()
    {
        var item = Novo();
        Assert.True(item.IncluidoNoRecurso);
    }

    [Fact]
    public void ExcluirDoRecurso_deve_marcar_IncluidoNoRecurso_false()
    {
        var item = Novo();
        item.ExcluirDoRecurso();
        Assert.False(item.IncluidoNoRecurso);
    }

    [Fact]
    public void ReincluirNoRecurso_deve_marcar_IncluidoNoRecurso_true_apos_excluir()
    {
        var item = Novo();
        item.ExcluirDoRecurso();
        item.ReincluirNoRecurso();
        Assert.True(item.IncluidoNoRecurso);
    }
}
