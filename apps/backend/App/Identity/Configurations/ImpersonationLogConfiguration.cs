using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Identity.Configurations;

internal sealed class ImpersonationLogConfiguration : IEntityTypeConfiguration<ImpersonationLog>
{
    public void Configure(EntityTypeBuilder<ImpersonationLog> builder)
    {
        builder.ToTable("ImpersonationLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.SaasUserId).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.StartedAt).IsRequired();
        builder.Property(l => l.EndedAt);

        builder.HasIndex(l => new { l.SaasUserId, l.TenantId });
    }
}
