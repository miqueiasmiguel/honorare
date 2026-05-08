using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class PassoCalculoConfiguration : IEntityTypeConfiguration<PassoCalculo>
{
    public void Configure(EntityTypeBuilder<PassoCalculo> builder)
    {
        builder.ToTable("passos_calculo");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CalculoId).IsRequired();
        builder.Property(p => p.ItemGuiaId).IsRequired();
        builder.Property(p => p.Sequencia).IsRequired();
        builder.Property(p => p.Regra).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Fator).HasColumnType("decimal(10,6)").IsRequired();
        builder.Property(p => p.ValorResultante).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasIndex(p => new { p.CalculoId, p.Sequencia }).IsUnique();

        builder.HasOne<Calculo>().WithMany()
            .HasForeignKey(p => p.CalculoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ItemGuia>().WithMany()
            .HasForeignKey(p => p.ItemGuiaId).OnDelete(DeleteBehavior.Restrict);
    }
}
