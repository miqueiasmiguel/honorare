using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class BeneficiarioConfiguration : IEntityTypeConfiguration<Beneficiario>
{
    public void Configure(EntityTypeBuilder<Beneficiario> builder)
    {
        builder.ToTable("beneficiarios");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.Carteira).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Nome).HasMaxLength(150).IsRequired();
        builder.Property(b => b.CriadoEm).IsRequired();

        builder.HasIndex(b => new { b.TenantId, b.Carteira }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.Nome });
    }
}
