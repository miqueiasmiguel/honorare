namespace App.Identity;

internal sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? LogoKey { get; private set; }

    private Tenant() { }

    public static Tenant Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Status = TenantStatus.Ativo,
        CreatedAt = DateTimeOffset.UtcNow
    };

    public void Activate() => Status = TenantStatus.Ativo;

    public void Suspend() => Status = TenantStatus.Suspenso;

    public void Cancel() => Status = TenantStatus.Cancelado;

    public void Rename(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        Name = trimmed;
    }

    public List<string> CodigosNaoRecorriveis { get; private set; } = [];

    public void SetLogoKey(string key) => LogoKey = key;

    public void ClearLogoKey() => LogoKey = null;

    public void DefinirCodigosNaoRecorriveis(IEnumerable<string> codigos)
    {
        CodigosNaoRecorriveis = codigos
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }
}
