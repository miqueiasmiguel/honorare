using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Faturamento.Tests.Fixtures;

/// <summary>
/// Starts a real PostgreSQL container for integration tests.
/// Share via xUnit's ICollectionFixture to spin up one container per test run.
/// </summary>
/// <example>
/// [Collection(nameof(PostgresCollection))]
/// public class MyIntegrationTests(PostgresContainerFixture db) { … }
/// </example>
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

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(PostgresCollection))]
#pragma warning disable CA1711 // xUnit collection marker classes must match the [Collection] attribute name
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
#pragma warning restore CA1711
