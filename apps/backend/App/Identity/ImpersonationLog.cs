namespace App.Identity;

internal sealed class ImpersonationLog
{
    public Guid Id { get; private set; }
    public Guid SaasUserId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    private ImpersonationLog() { }

    public static ImpersonationLog Create(Guid saasUserId, Guid tenantId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SaasUserId = saasUserId,
            TenantId = tenantId,
            StartedAt = DateTimeOffset.UtcNow
        };

    public void Close() => EndedAt = DateTimeOffset.UtcNow;
}
