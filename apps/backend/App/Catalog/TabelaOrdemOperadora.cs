using App.Identity;

namespace App.Catalog;

internal sealed class TabelaOrdemOperadora : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public int NumeroProcedimento { get; private set; }
    public TipoViaOrdem TipoVia { get; private set; }
    public decimal Percentual { get; private set; }

    private TabelaOrdemOperadora() { }

    public static TabelaOrdemOperadora Create(
        Guid tenantId,
        Guid operadoraId,
        int numeroProcedimento,
        TipoViaOrdem tipoVia,
        decimal percentual)
    {
        return new TabelaOrdemOperadora
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperadoraId = operadoraId,
            NumeroProcedimento = numeroProcedimento,
            TipoVia = tipoVia,
            Percentual = percentual,
        };
    }

    public void AtualizarPercentual(decimal percentual)
    {
        Percentual = percentual;
    }
}
