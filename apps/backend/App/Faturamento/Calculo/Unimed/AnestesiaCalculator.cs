namespace App.Faturamento.Motor.Unimed;

internal static class AnestesiaCalculator
{
    private static readonly Dictionary<int, int> _tempoBase = new()
    {
        [1] = 60,
        [2] = 90,
        [3] = 120,
        [4] = 150,
        [5] = 180,
        [6] = 240,
        [7] = 300,
        [8] = 360,
    };

    internal static (decimal valorFinal, IReadOnlyList<PassoApuracao> passos) Calcular(
        decimal valorTabela,
        decimal deflatorPercentual,
        int porteAnestesico,
        int? tempoAnestesicoMin,
        OrdemProcedimento ordem,
        Acomodacao acomodacao,
        bool ehUrgencia,
        bool ehSadt)
    {
        if (!_tempoBase.TryGetValue(porteAnestesico, out var tempoBaseMin))
        {
            throw new ArgumentOutOfRangeException(nameof(porteAnestesico), porteAnestesico, "Porte anestésico deve estar entre 1 e 8.");
        }

        var passos = new List<PassoApuracao>();

        var fatorBase = deflatorPercentual / 100m;
        var valorBase = valorTabela * fatorBase;
        passos.Add(new PassoApuracao("ValorBase", fatorBase, valorBase));

        var valorUnimed = valorBase * 1.1719m;
        passos.Add(new PassoApuracao("UnimedAN", 1.1719m, valorUnimed));

        var valorAtual = valorUnimed;

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

        var fatorAcomodacao = acomodacao == Acomodacao.Apartamento ? 2.0m : 1.0m;
        if (fatorAcomodacao != 1.0m)
        {
            valorAtual *= fatorAcomodacao;
            passos.Add(new PassoApuracao("Acomodacao", fatorAcomodacao, valorAtual));
        }

        var fatorUrgencia = (ehUrgencia && !ehSadt) ? 1.3m : 1.0m;
        if (fatorUrgencia != 1.0m)
        {
            valorAtual *= fatorUrgencia;
            passos.Add(new PassoApuracao("Urgencia", fatorUrgencia, valorAtual));
        }

        var acrescimoTempo = 0m;
        if (tempoAnestesicoMin.HasValue && tempoAnestesicoMin.Value > tempoBaseMin)
        {
            var extraHoras = (decimal)Math.Ceiling((tempoAnestesicoMin.Value - tempoBaseMin) / 60.0);
            var fatorExtra = porteAnestesico <= 4 ? 0.30m : 0.50m;
            acrescimoTempo = extraHoras * fatorExtra * valorUnimed;
            passos.Add(new PassoApuracao("TempoExtra", fatorExtra, valorAtual + acrescimoTempo));
        }

        return (valorAtual + acrescimoTempo, passos);
    }
}
