using App.Catalog;

namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class AcomodacaoModifier
{
    internal static PassoApuracao Aplicar(Acomodacao acomodacao, PosicaoExecutor posicao, decimal valorAtual)
    {
        var dobra = acomodacao == Acomodacao.Apartamento && posicao == PosicaoExecutor.Cirurgiao;
        var fator = dobra ? 2.0m : 1.0m;
        return new PassoApuracao("Acomodacao", fator, valorAtual * fator);
    }
}
