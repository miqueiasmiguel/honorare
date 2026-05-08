namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class UrgenciaModifier
{
    internal static PassoApuracao Aplicar(bool ehUrgencia, bool ehSadt, decimal valorAtual)
    {
        var fator = ehUrgencia && !ehSadt ? 1.3m : 1.0m;
        return new PassoApuracao("Urgencia", fator, valorAtual * fator);
    }
}
