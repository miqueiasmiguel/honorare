using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class ItemDemonstrativoConfiguration : IEntityTypeConfiguration<ItemDemonstrativo>
{
    public void Configure(EntityTypeBuilder<ItemDemonstrativo> builder)
    {
        builder.ToTable("itens_demonstrativo");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.DemonstrativoId).IsRequired();
        builder.Property(i => i.Senha).HasMaxLength(30).IsRequired();
        builder.Property(i => i.CodigoTuss).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Descricao).HasMaxLength(200);
        builder.Property(i => i.ValorApresentado).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(i => i.ValorPago).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(i => i.ValorGlosado).HasColumnType("decimal(12,2)").IsRequired();
        builder.Property(i => i.MotivoGlosa).HasMaxLength(500);
        builder.Property(i => i.ItemGuiaId);
        builder.Property(i => i.CriadoEm).IsRequired();

        builder.HasOne<Demonstrativo>().WithMany()
            .HasForeignKey(i => i.DemonstrativoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ItemGuia>().WithMany()
            .HasForeignKey(i => i.ItemGuiaId).OnDelete(DeleteBehavior.Restrict);
    }
}
