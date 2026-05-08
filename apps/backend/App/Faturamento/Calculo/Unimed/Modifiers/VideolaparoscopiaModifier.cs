namespace App.Faturamento.Motor.Unimed.Modifiers;

internal static class VideolaparoscopiaModifier
{
    internal static PassoApuracao Aplicar(ViaAcesso via, bool temPorteProprioVideo, decimal valorAtual)
    {
        var fator = via == ViaAcesso.Videolaparoscopia && !temPorteProprioVideo ? 1.5m : 1.0m;
        return new PassoApuracao("Videolaparoscopia", fator, valorAtual * fator);
    }
}
