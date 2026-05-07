using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class TabelaProcedimentoConfiguration : IEntityTypeConfiguration<TabelaProcedimento>
{
    public void Configure(EntityTypeBuilder<TabelaProcedimento> builder)
    {
        builder.ToTable("tabelas_procedimento");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.OperadoraId).IsRequired();
        builder.Property(t => t.ProcedimentoId).IsRequired();
        builder.Property(t => t.Valor).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(t => t.AtualizadoEm).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.OperadoraId, t.ProcedimentoId })
            .IsUnique();
    }
}
