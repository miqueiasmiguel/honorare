using App.Data;
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

    internal async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var ctx = new AppDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(IdentityPostgresCollection))]
#pragma warning disable CA1711
public sealed class IdentityPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
#pragma warning restore CA1711
