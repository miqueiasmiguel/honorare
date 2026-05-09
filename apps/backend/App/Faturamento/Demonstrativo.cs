using App.Identity;

namespace App.Faturamento;

internal sealed class Demonstrativo : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public string Competencia { get; private set; } = string.Empty;
    public DateOnly DataRecebimento { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Demonstrativo() { }

    internal static Demonstrativo Create(
        Guid tenantId,
        Guid operadoraId,
        string competencia,
        DateOnly dataRecebimento,
        string? observacao)
    {
        return new Demonstrativo
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperadoraId = operadoraId,
            Competencia = competencia.Trim(),
            DataRecebimento = dataRecebimento,
            Observacao = observacao?.Trim(),
            CriadoEm = DateTimeOffset.UtcNow,
        };
    }

    internal void Atualizar(
        Guid operadoraId,
        string competencia,
        DateOnly dataRecebimento,
        string? observacao)
    {
        OperadoraId = operadoraId;
        Competencia = competencia.Trim();
        DataRecebimento = dataRecebimento;
        Observacao = observacao?.Trim();
    }
}
