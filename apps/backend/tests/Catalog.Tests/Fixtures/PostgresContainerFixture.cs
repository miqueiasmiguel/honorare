using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Catalog.Tests.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("honorare_catalog_test")
        .WithUsername("honorare")
        .WithPassword("honorare")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    // MigrateAsync (não EnsureCreatedAsync) garante o contrato TDD:
    //   – antes da migration (TASK-CAT-01): tabelas do Catalog não existem → schema tests falham (RED)
    //   – após a migration  (TASK-CAT-02): tabelas criadas pelo script gerado → schema tests passam (GREEN)
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

    // Cria contexto filtrado por um tenant específico — útil nos testes de isolamento
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
