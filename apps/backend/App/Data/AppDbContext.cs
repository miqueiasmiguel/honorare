using System.Reflection;
using App.Catalog;
using App.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace App.Data;

internal class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private readonly ICurrentUser _currentUser = currentUser;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Operadora> Operadoras => Set<Operadora>();
    public DbSet<Procedimento> Procedimentos => Set<Procedimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            _applyTenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private static readonly MethodInfo _applyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(
            nameof(ApplyTenantFilterForEntity),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ApplyTenantFilterForEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => _currentUser.IsSaasAdmin || e.TenantId == _currentUser.TenantId);
    }
}
