using App.Data;
using App.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Identity.Tests.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("honorare_identity_test")
        .WithUsername("honorare")
        .WithPassword("honorare")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    internal async Task<AppDbContext> CreateContextAsync(ICurrentUser? currentUser = null)
    {
        currentUser ??= new SaasAdminCurrentUser();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, currentUser);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    internal async Task<AppDbContext> CreateImpersonationContextAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var ctx = new AppDbContext(options, new ImpersonatingCurrentUser(tenantId));
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    // Bypass total do filtro — usado como default em testes de schema.
    private sealed class SaasAdminCurrentUser : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? TenantId => null;
        public Guid? MedicoId => null;
        public bool IsSaasAdmin => true;
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

[CollectionDefinition(nameof(IdentityPostgresCollection))]
#pragma warning disable CA1711
public sealed class IdentityPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
#pragma warning restore CA1711
