namespace App.Faturamento.Motor.Unimed;

internal static class AnestesiaCalculator
{
    internal static (decimal valorFinal, IReadOnlyList<PassoApuracao> passos) Calcular(
        decimal valorReferencia,
        decimal deflatorPercentual,
        OrdemProcedimento ordem,
        bool ehUrgencia,
        bool ehSadt)
    {
        var passos = new List<PassoApuracao>();

        var fatorBase = deflatorPercentual / 100m;
        var valorAtual = valorReferencia * fatorBase;
        passos.Add(new PassoApuracao("ValorBase", fatorBase, valorAtual));

        var fatorOrdem = ordem switch
        {
            OrdemProcedimento.SecundarioMesmaVia => 0.5m,
            OrdemProcedimento.SecundarioViaDiferente => 0.7m,
            _ => 1.0m,
        };
        if (fatorOrdem != 1.0m)
        {
            valorAtual *= fatorOrdem;
            passos.Add(new PassoApuracao("OrdemProcedimento", fatorOrdem, valorAtual));
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
