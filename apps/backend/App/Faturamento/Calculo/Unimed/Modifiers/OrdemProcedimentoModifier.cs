namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class OrdemProcedimentoModifier
{
    internal static PassoApuracao Aplicar(OrdemProcedimento ordem, decimal valorAtual)
    {
        var fator = ordem switch
        {
            OrdemProcedimento.SecundarioMesmaVia => 0.5m,
            OrdemProcedimento.SecundarioViaDiferente => 0.7m,
            _ => 1.0m,
        };
        return new PassoApuracao("OrdemProcedimento", fator, valorAtual * fator);
    }
}
