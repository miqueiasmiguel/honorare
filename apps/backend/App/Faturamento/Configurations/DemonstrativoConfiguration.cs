using App.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class DemonstrativoConfiguration : IEntityTypeConfiguration<Demonstrativo>
{
    public void Configure(EntityTypeBuilder<Demonstrativo> builder)
    {
        builder.ToTable("demonstrativos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.OperadoraId).IsRequired();
        builder.Property(d => d.IdentificadorPagamento).HasMaxLength(50);
        builder.Property(d => d.Competencia).HasMaxLength(7).IsRequired();
        builder.Property(d => d.DataRecebimento).IsRequired();
        builder.Property(d => d.Observacao).HasMaxLength(2000);
        builder.Property(d => d.CriadoEm).IsRequired();

        builder.HasIndex(d => d.TenantId);

        builder.HasOne<Operadora>().WithMany()
            .HasForeignKey(d => d.OperadoraId).OnDelete(DeleteBehavior.Restrict);
    }
}
