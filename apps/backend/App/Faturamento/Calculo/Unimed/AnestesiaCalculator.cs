namespace App.Faturamento.Motor.Unimed;

internal static class AnestesiaCalculator
{
    internal static (decimal valorFinal, IReadOnlyList<PassoApuracao> passos) Calcular(
        decimal valorReferencia,
        decimal deflatorPercentual,
        decimal percentualOrdem,
        bool ehUrgencia,
        bool ehSadt)
    {
        var passos = new List<PassoApuracao>();

        var fatorBase = deflatorPercentual / 100m;
        var valorAtual = valorReferencia * fatorBase;
        passos.Add(new PassoApuracao("ValorBase", fatorBase, valorAtual));

        if (percentualOrdem != 1.0m)
        {
            valorAtual *= percentualOrdem;
            passos.Add(new PassoApuracao("OrdemProcedimento", percentualOrdem, valorAtual));
        }

        var fatorUrgencia = (ehUrgencia && !ehSadt) ? 1.3m : 1.0m;
        if (fatorUrgencia != 1.0m)
        {
            valorAtual *= fatorUrgencia;
            passos.Add(new PassoApuracao("Urgencia", fatorUrgencia, valorAtual));
        }

        return (valorAtual, passos);
    }
}
