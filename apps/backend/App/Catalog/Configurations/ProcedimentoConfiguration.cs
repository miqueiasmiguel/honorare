using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class ProcedimentoConfiguration : IEntityTypeConfiguration<Procedimento>
{
    public void Configure(EntityTypeBuilder<Procedimento> builder)
    {
        builder.ToTable("procedimentos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.CodigoTuss).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Porte).HasMaxLength(4);
        builder.Property(p => p.PorteAnestesico).HasColumnType("varchar(2)");
        builder.Property(p => p.EhSadt).IsRequired();
        builder.Property(p => p.TemPorteProprioVideo).IsRequired();
        builder.Property(p => p.Ativo).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.CodigoTuss })
            .IsUnique();

        builder.HasIndex(p => new { p.TenantId, p.Ativo });
    }
}
