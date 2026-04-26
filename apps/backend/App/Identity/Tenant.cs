namespace App.Identity;

internal sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Status = TenantStatus.Ativo,
        CreatedAt = DateTimeOffset.UtcNow
    };

    public void Suspend() => Status = TenantStatus.Suspenso;

    public void Cancel() => Status = TenantStatus.Cancelado;
}
