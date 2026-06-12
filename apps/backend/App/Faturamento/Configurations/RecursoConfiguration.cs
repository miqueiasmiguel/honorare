using App.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class RecursoConfiguration : IEntityTypeConfiguration<Recurso>
{
    public void Configure(EntityTypeBuilder<Recurso> builder)
    {
        builder.ToTable("recursos");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.OperadoraId).IsRequired();
        builder.Property(r => r.PrestadorId).IsRequired();
        builder.Property(r => r.Numero).HasMaxLength(20).IsRequired();
        builder.Property(r => r.DataEmissao).IsRequired();
        builder.Property(r => r.Observacao).HasMaxLength(2000);
        builder.Property(r => r.CriadoEm).IsRequired();
        builder.Property(r => r.Tipo).HasConversion<string>().IsRequired();

        builder.HasIndex(r => r.TenantId);

        builder.HasOne<Operadora>().WithMany()
            .HasForeignKey(r => r.OperadoraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prestador>().WithMany()
            .HasForeignKey(r => r.PrestadorId).OnDelete(DeleteBehavior.Restrict);
    }
}
