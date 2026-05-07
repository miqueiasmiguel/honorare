using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class DeflatorPrestadorConfiguration : IEntityTypeConfiguration<DeflatorPrestador>
{
    public void Configure(EntityTypeBuilder<DeflatorPrestador> builder)
    {
        builder.ToTable("deflatores_prestador");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.PrestadorId).IsRequired();
        builder.Property(d => d.OperadoraId).IsRequired();
        builder.Property(d => d.Posicao).HasConversion<string>().IsRequired();
        builder.Property(d => d.Percentual).HasColumnType("decimal(6,2)").IsRequired();

        builder.HasIndex(d => new { d.TenantId, d.PrestadorId, d.OperadoraId, d.Posicao })
            .IsUnique();
    }
}
