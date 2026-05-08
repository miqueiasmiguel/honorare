using App.Identity;

namespace App.Faturamento;

internal sealed class Calculo : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GuiaId { get; private set; }
    public DateTimeOffset RealizadoEm { get; private set; }

    private Calculo() { }

    internal static Calculo Create(Guid tenantId, Guid guiaId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GuiaId = guiaId,
            RealizadoEm = DateTimeOffset.UtcNow,
        };
}
