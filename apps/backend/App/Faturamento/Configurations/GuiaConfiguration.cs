using App.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Faturamento.Configurations;

internal sealed class GuiaConfiguration : IEntityTypeConfiguration<Guia>
{
    public void Configure(EntityTypeBuilder<Guia> builder)
    {
        builder.ToTable("guias");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.TenantId).IsRequired();
        builder.Property(g => g.PrestadorId).IsRequired();
        builder.Property(g => g.OperadoraId).IsRequired();
        builder.Property(g => g.BeneficiarioId);
        builder.Property(g => g.Senha).HasMaxLength(30).IsRequired();
        builder.Property(g => g.DataAtendimento).IsRequired();
        builder.Property(g => g.Situacao).HasConversion<string>().IsRequired();
        builder.Property(g => g.EhPacote).IsRequired();
        builder.Property(g => g.Observacao).HasMaxLength(2000).IsRequired();
        builder.Property(g => g.CriadoEm).IsRequired();
        builder.Property(g => g.AtualizadoEm).IsRequired();

        builder.HasIndex(g => g.TenantId);

        builder.HasOne<Prestador>().WithMany()
            .HasForeignKey(g => g.PrestadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Operadora>().WithMany()
            .HasForeignKey(g => g.OperadoraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Beneficiario>().WithMany()
            .HasForeignKey(g => g.BeneficiarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
