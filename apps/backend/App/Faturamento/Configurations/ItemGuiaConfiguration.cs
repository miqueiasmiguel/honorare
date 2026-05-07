using App.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class ItemGuiaConfiguration : IEntityTypeConfiguration<ItemGuia>
{
    public void Configure(EntityTypeBuilder<ItemGuia> builder)
    {
        builder.ToTable("itens_guia");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.GuiaId).IsRequired();
        builder.Property(i => i.ProcedimentoId).IsRequired();
        builder.Property(i => i.PosicaoExecutor).HasConversion<string>().IsRequired();
        builder.Property(i => i.OrdemProcedimento).HasConversion<string>().IsRequired();
        builder.Property(i => i.ViaAcesso).HasConversion<string>().IsRequired();
        builder.Property(i => i.Acomodacao).HasConversion<string>().IsRequired();
        builder.Property(i => i.EhUrgencia).IsRequired();
        builder.Property(i => i.ValorApurado).HasColumnType("decimal(12,2)");
        builder.Property(i => i.ValorLiquidado).HasColumnType("decimal(12,2)");
        builder.Property(i => i.CriadoEm).IsRequired();

        builder.HasOne<Guia>().WithMany()
            .HasForeignKey(i => i.GuiaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Procedimento>().WithMany()
            .HasForeignKey(i => i.ProcedimentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
