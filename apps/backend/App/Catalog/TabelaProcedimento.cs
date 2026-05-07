using App.Identity;

namespace App.Catalog;

internal sealed class TabelaProcedimento : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public Guid ProcedimentoId { get; private set; }
    public decimal Valor { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private TabelaProcedimento() { }

    public static TabelaProcedimento Create(
        Guid tenantId, Guid operadoraId, Guid procedimentoId, decimal valor)
    {
        return new TabelaProcedimento
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperadoraId = operadoraId,
            ProcedimentoId = procedimentoId,
            Valor = valor,
            AtualizadoEm = DateTimeOffset.UtcNow
        };
    }

    public void AtualizarValor(decimal valor)
    {
        Valor = valor;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }
}
