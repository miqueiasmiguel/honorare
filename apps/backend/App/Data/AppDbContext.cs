using System.Reflection;
using App.Catalog;
using App.Faturamento;
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
    public DbSet<Prestador> Prestadores => Set<Prestador>();
    public DbSet<TabelaProcedimento> TabelasProcedimento => Set<TabelaProcedimento>();
    public DbSet<DeflatorPrestador> DeflatoresPrestador => Set<DeflatorPrestador>();
    public DbSet<Beneficiario> Beneficiarios => Set<Beneficiario>();
    public DbSet<TabelaPorteAnestesico> TabelasPorteAnestesico => Set<TabelaPorteAnestesico>();
    public DbSet<TabelaOrdemOperadora> TabelasOrdemOperadora => Set<TabelaOrdemOperadora>();
    public DbSet<Guia> Guias => Set<Guia>();
    public DbSet<ItemGuia> ItensGuia => Set<ItemGuia>();
    public DbSet<Calculo> Calculos => Set<Calculo>();
    public DbSet<PassoCalculo> PassosCalculo => Set<PassoCalculo>();
    public DbSet<Recurso> Recursos => Set<Recurso>();

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
            .HasQueryFilter(e =>
                (_currentUser.IsSaasAdmin && _currentUser.TenantId == null)
                || e.TenantId == _currentUser.TenantId);
    }
}
