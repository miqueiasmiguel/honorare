namespace App.Faturamento;

internal sealed class ItemDemonstrativo
{
    public Guid Id { get; private set; }
    public Guid DemonstrativoId { get; private set; }
    public string Senha { get; private set; } = string.Empty;
    public string CodigoTuss { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public decimal ValorApresentado { get; private set; }
    public decimal ValorPago { get; private set; }
    public decimal ValorGlosado { get; private set; }
    public string? MotivoGlosa { get; private set; }
    public Guid? ItemGuiaId { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private ItemDemonstrativo() { }

    internal static ItemDemonstrativo Create(
        Guid demonstrativoId,
        string senha,
        string codigoTuss,
        string? descricao,
        decimal valorApresentado,
        decimal valorPago,
        string? motivoGlosa)
    {
        return new ItemDemonstrativo
        {
            Id = Guid.NewGuid(),
            DemonstrativoId = demonstrativoId,
            Senha = senha.Trim(),
            CodigoTuss = codigoTuss.Trim(),
            Descricao = descricao?.Trim(),
            ValorApresentado = valorApresentado,
            ValorPago = valorPago,
            ValorGlosado = valorApresentado - valorPago,
            MotivoGlosa = motivoGlosa?.Trim(),
            CriadoEm = DateTimeOffset.UtcNow,
        };
    }

    internal void Conciliar(Guid itemGuiaId) => ItemGuiaId = itemGuiaId;

    internal void Desconciliar() => ItemGuiaId = null;
}
