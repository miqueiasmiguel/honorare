using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Identity.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.LogoKey).HasMaxLength(512);
        builder.Property(t => t.CodigosNaoRecorriveis).HasColumnType("text[]");
    }
}
