using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class OperadoraConfiguration : IEntityTypeConfiguration<Operadora>
{
    public void Configure(EntityTypeBuilder<Operadora> builder)
    {
        builder.ToTable("operadoras");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.TenantId).IsRequired();
        builder.Property(o => o.Nome).HasMaxLength(200).IsRequired();
        builder.Property(o => o.RegistroAns).HasMaxLength(6);
        builder.Property(o => o.Cnpj).HasMaxLength(14);
        builder.Property(o => o.TipoRuleSet).HasConversion<string>().IsRequired();
        builder.Property(o => o.Ativa).IsRequired();
        builder.Property(o => o.CriadaEm).IsRequired();

        // CNPJ único por tenant apenas quando informado — NULL não viola unicidade no PostgreSQL
        builder.HasIndex(o => new { o.TenantId, o.Cnpj })
            .IsUnique()
            .HasFilter("\"Cnpj\" IS NOT NULL");

        builder.HasIndex(o => new { o.TenantId, o.Ativa });
    }
}
