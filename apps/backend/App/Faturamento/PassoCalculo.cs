namespace App.Faturamento;

internal sealed class PassoCalculo
{
    public Guid Id { get; private set; }
    public Guid CalculoId { get; private set; }
    public Guid ItemGuiaId { get; private set; }
    public int Sequencia { get; private set; }
    public string Regra { get; private set; } = string.Empty;
    public decimal Fator { get; private set; }
    public decimal ValorResultante { get; private set; }

    private PassoCalculo() { }

    internal static PassoCalculo Create(
        Guid calculoId, Guid itemGuiaId, int seq,
        string regra, decimal fator, decimal valorResultante) =>
        new()
        {
            Id = Guid.NewGuid(),
            CalculoId = calculoId,
            ItemGuiaId = itemGuiaId,
            Sequencia = seq,
            Regra = regra,
            Fator = fator,
            ValorResultante = valorResultante,
        };
}
