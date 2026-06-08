namespace App.Faturamento.Motor.Unimed;

internal static class AnestesiaCalculator
{
    internal static (decimal valorFinal, IReadOnlyList<PassoApuracao> passos) Calcular(
        decimal valorReferencia,
        decimal percentualOrdem,
        bool ehUrgencia,
        bool ehSadt)
    {
        var passos = new List<PassoApuracao>();

        var valorAtual = valorReferencia;
        passos.Add(new PassoApuracao("ValorBase", 1.0m, valorAtual));

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
