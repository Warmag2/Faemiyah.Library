using System.Collections.Generic;
using System.Threading;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Options;
using Faemiyah.Repository.Providers;
using Faemiyah.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NUnit.Framework;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Faemiyah.Repository.IntegrationTests;

/// <summary>
/// Gives each test its own database inside the shared container.
/// </summary>
/// <remarks>
/// Per-test databases give complete isolation without truncation scripts or rollback tricks, and mean every test
/// also exercises the provisioning path. The library is built for one database per tenant, so this is the same
/// mechanism it exists to provide.
/// </remarks>
public abstract class IntegrationTestBase
{
    /// <summary>
    /// The pool ceiling given to every per-test database, kept low because there are many of them.
    /// </summary>
    private const int MaxPoolSizePerDatabase = 4;

    private static int _databaseCounter;

    protected TestDatabaseContextProvider ContextProvider { get; private set; }

    protected TestEntityRepository Repository { get; private set; }

    protected TestUuidEntityRepository UuidRepository { get; private set; }

    protected TestTemporalEntityRepository TemporalRepository { get; private set; }

    [SetUp]
    public void CreatePerTestDatabase()
    {
        var dbName = $"test_{Interlocked.Increment(ref _databaseCounter)}";

        ContextProvider = CreateProvider(dbName);

        Repository = new TestEntityRepository(
            NullLogger<RepositoryBase<TestDbContext, long, TestEntity>>.Instance,
            ContextProvider);

        UuidRepository = new TestUuidEntityRepository(
            NullLogger<RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>>.Instance,
            ContextProvider);

        TemporalRepository = new TestTemporalEntityRepository(
            NullLogger<TemporalRepositoryBase<TestDbContext, long, TestTemporalEntity>>.Instance,
            ContextProvider);
    }

    /// <summary>
    /// Releases the pool belonging to the database this test used.
    /// </summary>
    /// <remarks>
    /// The database is never touched again, so its pooled connections are pure waste. Without this the pools of
    /// every completed test stay open and the suite eventually exhausts the server's connection limit.
    /// </remarks>
    [TearDown]
    public void ReleasePooledConnections()
    {
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>
    /// Builds a provider wired to the container, going through the library's own connection string accessor rather
    /// than a test double, so that its behaviour is covered too.
    /// </summary>
    /// <param name="defaultDbName">The database the provider uses when no name is supplied, or <b>null</b> for none.</param>
    /// <param name="maxPoolSize">
    /// The pool ceiling. Raise it only for a test which needs callers to genuinely overlap, since the default is
    /// kept low so that the suite as a whole does not exhaust the server.
    /// </param>
    /// <returns>The provider.</returns>
    protected static TestDatabaseContextProvider CreateProvider(
        string? defaultDbName,
        int maxPoolSize = MaxPoolSizePerDatabase)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = PostgreSqlFixture.BaseConnectionString
            })
            .Build();

        // Every test uses its own database, so the suite accumulates one pool per test. At the provider default of
        // 100 per pool that exhausts the server's connection limit part way through the run.
        var options = MsOptions.Create(new SqlOptions
        {
            DefaultDbName = defaultDbName,
            MaxPoolSize = maxPoolSize
        });

        return new TestDatabaseContextProvider(
            NullLogger<PgSqlDatabaseContextProvider<TestDbContext>>.Instance,
            options,
            new PgSqlConnectionStringAccessor(configuration, options));
    }
}
