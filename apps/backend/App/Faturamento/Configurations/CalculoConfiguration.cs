using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class CalculoConfiguration : IEntityTypeConfiguration<Calculo>
{
    public void Configure(EntityTypeBuilder<Calculo> builder)
    {
        builder.ToTable("calculos");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.GuiaId).IsRequired();
        builder.Property(c => c.RealizadoEm).IsRequired();

        builder.HasIndex(c => c.GuiaId);

        builder.HasOne<Guia>().WithMany()
            .HasForeignKey(c => c.GuiaId).OnDelete(DeleteBehavior.Restrict);
    }
}
