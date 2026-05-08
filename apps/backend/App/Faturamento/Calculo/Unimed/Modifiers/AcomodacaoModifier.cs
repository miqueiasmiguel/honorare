namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class AcomodacaoModifier
{
    internal static PassoApuracao Aplicar(Acomodacao acomodacao, decimal valorAtual)
    {
        var fator = acomodacao == Acomodacao.Apartamento ? 2.0m : 1.0m;
        return new PassoApuracao("Acomodacao", fator, valorAtual * fator);
    }
}
