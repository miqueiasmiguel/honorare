using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class PrestadorConfiguration : IEntityTypeConfiguration<Prestador>
{
    public void Configure(EntityTypeBuilder<Prestador> builder)
    {
        builder.ToTable("prestadores");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.RegistroProfissional).HasMaxLength(20);
        builder.Property(p => p.Ativo).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();
        builder.Property(p => p.EmailAcesso).HasMaxLength(256);

        builder.HasIndex(p => new { p.TenantId, p.Ativo });
    }
}
