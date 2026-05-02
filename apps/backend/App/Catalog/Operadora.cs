using App.Identity;

namespace App.Catalog;

internal sealed class Operadora : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? RegistroAns { get; private set; }
    public string? Cnpj { get; private set; }
    public TipoRuleSet TipoRuleSet { get; private set; }
    public bool Ativa { get; private set; }
    public DateTimeOffset CriadaEm { get; private set; }

    private Operadora() { }

    public static Operadora Create(
        Guid tenantId,
        string nome,
        string? registroAns,
        string? cnpj,
        TipoRuleSet tipoRuleSet)
    {
        return new Operadora
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nome = nome,
            RegistroAns = registroAns,
            Cnpj = cnpj,
            TipoRuleSet = tipoRuleSet,
            Ativa = true,
            CriadaEm = DateTimeOffset.UtcNow
        };
    }

    public void Atualizar(string nome, string? registroAns, string? cnpj, TipoRuleSet tipoRuleSet, bool ativa)
    {
        Nome = nome;
        RegistroAns = registroAns;
        Cnpj = cnpj;
        TipoRuleSet = tipoRuleSet;
        Ativa = ativa;
    }
}
