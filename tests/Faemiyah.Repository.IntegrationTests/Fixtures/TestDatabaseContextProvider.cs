using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Faemiyah.Repository.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A concrete context provider for the test context.
/// </summary>
/// <remarks>
/// The schema is created with <c>EnsureCreatedAsync</c> rather than migrations, so that the model can change without
/// regenerating migration files. It still goes through the same model to DDL path, so the unique index on
/// <c>UniqueId</c> and the exclusion of the <c>xmin</c> system column are both still exercised.
/// </remarks>
public class TestDatabaseContextProvider : PgSqlDatabaseContextProvider<TestDbContext>
{
    private int _provisioningCount;

    public TestDatabaseContextProvider(
        ILogger<PgSqlDatabaseContextProvider<TestDbContext>> logger,
        IOptions<SqlOptions> sqlOptions,
        IConnectionStringAccessor connectionStringAccessor)
        : base(logger, sqlOptions, connectionStringAccessor)
    {
    }

    /// <summary>
    /// How many times provisioning has run. Lets a test assert that it happens once per database, not once per call.
    /// </summary>
    public int ProvisioningCount => Volatile.Read(ref _provisioningCount);

    /// <summary>
    /// When set, provisioning throws on its first attempt. Used to check that a failure is not cached.
    /// </summary>
    public bool FailNextProvisioning { get; set; }

    protected override TestDbContext GetContextInstance(DbContextOptions<TestDbContext> dbContextOptions)
    {
        return new TestDbContext(dbContextOptions);
    }

    protected override async Task OnDatabaseProvisionedAsync(
        TestDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _provisioningCount);

        if (FailNextProvisioning)
        {
            FailNextProvisioning = false;

            throw new TestProvisioningException();
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
