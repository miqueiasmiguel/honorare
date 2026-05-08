using App.Catalog;

namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class PosicaoExecutorModifier
{
    internal static PassoApuracao Aplicar(PosicaoExecutor posicao, decimal valorAtual)
    {
        var fator = posicao switch
        {
            PosicaoExecutor.PrimeiroAuxiliar => 0.6m,
            PosicaoExecutor.SegundoAuxiliar => 0.4m,
            PosicaoExecutor.TerceiroAuxiliar => 0.3m,
            _ => 1.0m,
        };
        return new PassoApuracao("PosicaoExecutor", fator, valorAtual * fator);
    }
}
