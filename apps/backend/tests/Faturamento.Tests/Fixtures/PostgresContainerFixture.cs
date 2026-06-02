using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Faturamento.Tests.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("honorare_test")
        .WithUsername("honorare")
        .WithPassword("honorare")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DbContextOptions<TContext> BuildOptions<TContext>()
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString)
            .Options;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    internal AppDbContext CreateContext(ICurrentUser? currentUser = null)
    {
        currentUser ??= new SaasAdminCurrentUser();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options, currentUser);
    }

    internal AppDbContext CreateTenantContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options, new TenantCurrentUser(tenantId));
    }

    internal AppDbContext CreateImpersonationContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options, new ImpersonatingCurrentUser(tenantId));
    }

    private sealed class SaasAdminCurrentUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => null;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => true;
        public bool IsImpersonating => false;
        public bool IsAuthenticated => true;
    }

    private sealed class TenantCurrentUser(Guid tenantId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => tenantId;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => false;
        public bool IsImpersonating => false;
        public bool IsAuthenticated => true;
    }

    private sealed class ImpersonatingCurrentUser(Guid tenantId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => tenantId;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => true;
        public bool IsImpersonating => true;
        public bool IsAuthenticated => true;
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
#pragma warning disable CA1711
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
#pragma warning restore CA1711
