using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Catalog.Configurations;

internal sealed class TabelaPorteAnestesicoConfiguration : IEntityTypeConfiguration<TabelaPorteAnestesico>
{
    public void Configure(EntityTypeBuilder<TabelaPorteAnestesico> builder)
    {
        builder.ToTable("tabelas_porte_anestesico");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.OperadoraId).IsRequired();
        builder.Property(t => t.PorteLetra).HasMaxLength(2).IsRequired();
        builder.Property(t => t.ValorEnfermaria).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(t => t.ValorApartamento).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(t => t.ValorAmbulatorial).HasColumnType("decimal(18,4)");
        builder.Property(t => t.AtualizadoEm).IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.OperadoraId, t.PorteLetra }).IsUnique();
    }
}
