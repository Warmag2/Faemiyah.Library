using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Faemiyah.Repository.IntegrationTests;

/// <summary>
/// Starts one PostgreSQL container for the whole assembly.
/// </summary>
/// <remarks>
/// Starting a container costs seconds, so it is shared. Isolation between tests comes from each test provisioning
/// its own database inside the container, which is also how the library is meant to be used.
/// </remarks>
[SetUpFixture]
public class PostgreSqlFixture
{
    /// <summary>
    /// The container image. Pinned so a new PostgreSQL release cannot change test outcomes silently.
    /// </summary>
    private const string PostgreSqlImage = "postgres:17-alpine";

    private static PostgreSqlContainer _container;

    /// <summary>
    /// A connection string pointing at the container. The database it names is rewritten per test.
    /// </summary>
    public static string BaseConnectionString { get; private set; }

    [OneTimeSetUp]
    public async Task StartContainerAsync()
    {
        _container = new PostgreSqlBuilder(PostgreSqlImage).Build();

        await _container.StartAsync();

        BaseConnectionString = _container.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task StopContainerAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
