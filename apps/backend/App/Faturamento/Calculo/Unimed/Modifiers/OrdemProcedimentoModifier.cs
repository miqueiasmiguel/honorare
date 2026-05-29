namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class OrdemProcedimentoModifier
{
    internal static PassoApuracao Aplicar(decimal percentualOrdem, decimal valorAtual)
    {
        return new PassoApuracao("OrdemProcedimento", percentualOrdem, valorAtual * percentualOrdem);
    }
}
