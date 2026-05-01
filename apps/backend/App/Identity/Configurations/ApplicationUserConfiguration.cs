using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Identity.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasIndex(u => u.GoogleId)
               .IsUnique()
               .HasFilter("\"GoogleId\" IS NOT NULL");

        builder.Property(u => u.GoogleId).HasMaxLength(128);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.Nome).HasMaxLength(100);
    }
}
