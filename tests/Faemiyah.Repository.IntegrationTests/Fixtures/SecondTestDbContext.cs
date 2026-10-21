using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Faemiyah.Repository.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A second context type, so that registering the infrastructure for more than one can be exercised.
/// </summary>
public class SecondTestDbContext : DbContext
{
    public SecondTestDbContext(DbContextOptions<SecondTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();
}

/// <summary>
/// A concrete context provider for <see cref="SecondTestDbContext"/>.
/// </summary>
public class SecondTestDatabaseContextProvider : PgSqlDatabaseContextProvider<SecondTestDbContext>
{
    public SecondTestDatabaseContextProvider(
        ILogger<PgSqlDatabaseContextProvider<SecondTestDbContext>> logger,
        IOptions<SqlOptions> sqlOptions,
        IConnectionStringAccessor connectionStringAccessor)
        : base(logger, sqlOptions, connectionStringAccessor)
    {
    }

    protected override SecondTestDbContext GetContextInstance(DbContextOptions<SecondTestDbContext> dbContextOptions)
    {
        return new SecondTestDbContext(dbContextOptions);
    }
}
