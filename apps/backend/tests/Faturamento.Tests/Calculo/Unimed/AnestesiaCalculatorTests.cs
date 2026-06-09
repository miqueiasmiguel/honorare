using App.Faturamento.Motor.Unimed;

namespace Faturamento.Tests.Motor.Unimed;

public sealed class AnestesiaCalculatorTests
{
    [Fact]
    public void Basico_Enfermaria_SemUrgencia()
    {
        var (valor, passos) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 1.0m,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(526.50m, valor);
        Assert.Contains(passos, p => p.Regra == "ValorBase");
        Assert.DoesNotContain(passos, p => p.Regra == "UnimedAN");
        Assert.DoesNotContain(passos, p => p.Regra == "TempoExtra");
    }

    [Fact]
    public void ComUrgencia()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 1.0m,
            ehUrgencia: true,
            ehSadt: false);

        Assert.Equal(684.45m, valor);
    }

    [Fact]
    public void UrgenciaEmSadt_NaoAplica()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 1.0m,
            ehUrgencia: true,
            ehSadt: true);

        Assert.Equal(526.50m, valor);
    }

    [Fact]
    public void SecundarioMesmaVia()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 0.5m,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(263.25m, valor);
    }

    [Fact]
    public void SecundarioViaDiferente()
    {
        var (valor, _) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 0.7m,
            ehUrgencia: false,
            ehSadt: false);

        Assert.Equal(368.55m, valor);
    }

    [Fact]
    public void Trace_ContemPassosAplicados()
    {
        var (_, passos) = AnestesiaCalculator.Calcular(
            valorReferencia: 526.50m,
            percentualOrdem: 0.5m,
            ehUrgencia: true,
            ehSadt: false);

        var regras = passos.Select(p => p.Regra).ToList();
        Assert.Contains("ValorBase", regras);
        Assert.Contains("OrdemProcedimento", regras);
        Assert.Contains("Urgencia", regras);
        Assert.DoesNotContain("Acomodacao", regras);
        Assert.DoesNotContain("UnimedAN", regras);
        Assert.DoesNotContain("TempoExtra", regras);
    }
}
