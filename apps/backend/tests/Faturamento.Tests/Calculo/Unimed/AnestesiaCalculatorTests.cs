using App.Faturamento;
using App.Faturamento.Motor.Unimed;

namespace Faturamento.Tests.Motor.Unimed;

public sealed class AnestesiaCalculatorTests
{
    [Fact]
    public void Basico_SemTempoExtra_Enfermaria()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 180,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(1171.90m, valor);
    }

    [Fact]
    public void ComMultiplicadorApartamento()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 180,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Apartamento,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(2343.80m, valor);
    }

    [Fact]
    public void ComUrgencia()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 180,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: true,
            ehSadt: false);

        Assert.Equal(1523.47m, valor);
    }

    [Fact]
    public void ComTempoExtraPA5_UmaHora()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 240,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(1757.85m, valor);
    }

    [Fact]
    public void ComTempoExtraPA3_UmaHora()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 3,
            tempoAnestesicoMin: 180,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(1523.47m, valor);
    }

    [Fact]
    public void TempoNulo_NaoAplicaAcrescimo()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: null,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(1171.90m, valor);
    }

    [Fact]
    public void TempoMenorQueBase_NaoAplicaAcrescimo()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 120,
            ordem: OrdemProcedimento.Unico,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(1171.90m, valor);
    }

    [Fact]
    public void OrdemSecundarioMesmaVia_Aplicada()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 180,
            ordem: OrdemProcedimento.SecundarioMesmaVia,
            acomodacao: Acomodacao.Enfermaria,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(585.95m, valor);
    }

    [Fact]
    public void Trace_ContemTodosPassosAplicados()
    {
        var (_, passos) = AnestesiaCalculator.Calcular(
            valorTabela: 1000m,
            deflatorPercentual: 100m,
            porteAnestesico: 5,
            tempoAnestesicoMin: 240,
            ordem: OrdemProcedimento.SecundarioMesmaVia,
            acomodacao: Acomodacao.Apartamento,
            ehUrgencia: true,
            ehSadt: false);

        var regras = passos.Select(p => p.Regra).ToList();
        Assert.Contains("ValorBase", regras);
        Assert.Contains("UnimedAN", regras);
        Assert.Contains("OrdemProcedimento", regras);
        Assert.Contains("Acomodacao", regras);
        Assert.Contains("Urgencia", regras);
        Assert.Contains("TempoExtra", regras);
    }

    [Fact]
    public void PaForaDaFaixa_LancaArgumentException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AnestesiaCalculator.Calcular(
                valorTabela: 1000m,
                deflatorPercentual: 100m,
                porteAnestesico: 9,
                tempoAnestesicoMin: null,
                ordem: OrdemProcedimento.Unico,
                acomodacao: Acomodacao.Enfermaria,
                ehUrgencia: false,
                ehSadt: false));
    }
}
