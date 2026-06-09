using App.Identity;

namespace App.Faturamento;

internal sealed class Recurso : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public Guid PrestadorId { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public DateOnly DataEmissao { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Recurso() { }

    internal static Recurso Create(
        Guid tenantId,
        Guid operadoraId,
        Guid prestadorId,
        DateOnly dataEmissao,
        string? observacao,
        string numero)
    {
        return new Recurso
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperadoraId = operadoraId,
            PrestadorId = prestadorId,
            Numero = numero.Trim(),
            DataEmissao = dataEmissao,
            Observacao = observacao?.Trim(),
            CriadoEm = DateTimeOffset.UtcNow,
        };
    }

    internal void Atualizar(
        Guid operadoraId,
        Guid prestadorId,
        DateOnly dataEmissao,
        string? observacao,
        string numero)
    {
        OperadoraId = operadoraId;
        PrestadorId = prestadorId;
        DataEmissao = dataEmissao;
        Numero = numero.Trim();
        Observacao = observacao?.Trim();
    }
}
