using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class TabelaOrdemOperadoraConfiguration : IEntityTypeConfiguration<TabelaOrdemOperadora>
{
    public void Configure(EntityTypeBuilder<TabelaOrdemOperadora> builder)
    {
        builder.ToTable("tabelas_ordem_operadora");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.OperadoraId).IsRequired();
        builder.Property(t => t.NumeroProcedimento).IsRequired();
        builder.Property(t => t.TipoVia).IsRequired();
        builder.Property(t => t.Percentual).HasColumnType("decimal(5,4)").IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.OperadoraId, t.NumeroProcedimento, t.TipoVia })
            .IsUnique();
    }
}
