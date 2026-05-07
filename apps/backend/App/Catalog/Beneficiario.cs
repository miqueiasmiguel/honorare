using App.Identity;

namespace App.Catalog;

internal sealed class Beneficiario : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Carteira { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }

    private Beneficiario() { }

    internal static Beneficiario Create(Guid tenantId, string carteira, string nome)
    {
        return new Beneficiario
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Carteira = carteira.Trim().ToUpperInvariant(),
            Nome = nome.Trim(),
            CriadoEm = DateTimeOffset.UtcNow
        };
    }

    internal void Atualizar(string nome)
    {
        Nome = nome.Trim();
    }
}
