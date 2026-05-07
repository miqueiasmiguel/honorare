using App.Identity;

namespace App.Catalog;

internal sealed class DeflatorPrestador : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PrestadorId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public PosicaoExecutor Posicao { get; private set; }
    public decimal Percentual { get; private set; }

    private DeflatorPrestador() { }

    public static DeflatorPrestador Create(
        Guid tenantId, Guid prestadorId, Guid operadoraId, PosicaoExecutor posicao, decimal percentual)
    {
        return new DeflatorPrestador
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrestadorId = prestadorId,
            OperadoraId = operadoraId,
            Posicao = posicao,
            Percentual = percentual
        };
    }

    public void AtualizarPercentual(decimal percentual)
    {
        Percentual = percentual;
    }
}
