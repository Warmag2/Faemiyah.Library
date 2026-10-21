using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.Extensions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class ServiceRegistrationTests
{
    private const int MaxPoolSizePerDatabase = 4;

    /// <summary>
    /// How many callers hit one repository instance at once. Deliberately larger than the pool, so callers also
    /// queue for connections rather than each getting one immediately.
    /// </summary>
    private const int ConcurrentCallers = 10;

    private static int _databaseCounter;

    /// <remarks>
    /// The provider records which databases it has provisioned, so a lifetime shorter than singleton would make it
    /// repeat provisioning for every scope. Resolving from two scopes has to yield the same instance.
    /// </remarks>
    [Test]
    public void AddPgSqlRepositoryProvider_RegistersProviderAsSingleton()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider($"test_di_{++_databaseCounter}");

        // Act
        IDatabaseContextProvider<TestDbContext> fromFirstScope;
        IDatabaseContextProvider<TestDbContext> fromSecondScope;

        using (var scope = serviceProvider.CreateScope())
        {
            fromFirstScope = scope.ServiceProvider.GetRequiredService<IDatabaseContextProvider<TestDbContext>>();
        }

        using (var scope = serviceProvider.CreateScope())
        {
            fromSecondScope = scope.ServiceProvider.GetRequiredService<IDatabaseContextProvider<TestDbContext>>();
        }

        // Assert
        fromFirstScope.Should().BeSameAs(fromSecondScope);
    }

    [Test]
    public void AddPgSqlRepositoryProvider_RegistersAccessorAsSingleton()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider($"test_di_{++_databaseCounter}");

        // Act
        var first = serviceProvider.GetRequiredService<IConnectionStringAccessor>();
        var second = serviceProvider.GetRequiredService<IConnectionStringAccessor>();

        // Assert
        first.Should().BeSameAs(second);
    }

    [Test]
    public void AddPgSqlRepositoryProvider_BindsTheConfiguredOptions()
    {
        // Arrange
        var dbName = $"test_di_{++_databaseCounter}";

        using var serviceProvider = BuildServiceProvider(dbName);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SqlOptions>>();

        // Assert
        options.Value.DefaultDbName.Should().Be(dbName);
        options.Value.MaxPoolSize.Should().Be(MaxPoolSizePerDatabase);
    }

    /// <remarks>
    /// The point of the whole registration: a repository injected from the container works without the consumer
    /// having wired the accessor, the options or the provider by hand.
    /// </remarks>
    [Test]
    public async Task ResolvedRepository_WorksAgainstTheDatabase()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider($"test_di_{++_databaseCounter}");

        var repository = serviceProvider.GetRequiredService<IEntityRepository<long, TestEntity>>();

        // Act
        var added = (await repository.AddAsync(new TestEntity { Name = "resolved from the container", Value = 7 }))
            .ShouldSucceedAs(OperationStatus.Added);
        var fetched = (await repository.GetAsync(e => e.Id == added.Id)).ShouldExist();

        // Assert
        fetched.Name.Should().Be("resolved from the container");
        fetched.Value.Should().Be(7);
    }

    /// <remarks>
    /// The justification for registering repositories as singletons. One instance is hit concurrently, and every
    /// call has to succeed with its own context; shared mutable state would show up as lost or corrupted writes.
    /// </remarks>
    [Test]
    public async Task ResolvedSingletonRepository_UsedConcurrently_ServesEveryCall()
    {
        // Arrange
        using var serviceProvider = BuildServiceProvider($"test_di_{++_databaseCounter}");

        var repository = serviceProvider.GetRequiredService<IEntityRepository<long, TestEntity>>();
        var second = serviceProvider.GetRequiredService<IEntityRepository<long, TestEntity>>();

        second.Should().BeSameAs(repository, "the repository is registered as a singleton");

        // Act
        var added = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentCallers)
                .Select(i => repository.AddAsync(new TestEntity { Name = $"concurrent-{i}", Value = i })));

        // Assert
        added.Select(result => result.ShouldSucceedAs(OperationStatus.Added).Id).Should().OnlyHaveUniqueItems();

        var stored = await repository.GetAllAsync();

        stored.Should().HaveCount(ConcurrentCallers);
    }

    /// <remarks>
    /// The accessor is shared on purpose, so that registering a second context type does not build a second one. The
    /// options are process-wide for the same reason, so both calls have to ask for the same ones.
    /// </remarks>
    [Test]
    public void AddPgSqlRepositoryProvider_CalledForOneContextTwice_SharesOneAccessor()
    {
        // Arrange
        var dbName = $"test_di_{++_databaseCounter}";

        var services = new ServiceCollection();

        AddInfrastructure(services, dbName);
        AddInfrastructure(services, dbName);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var accessors = serviceProvider.GetServices<IConnectionStringAccessor>();

        // Assert
        accessors.Should().HaveCount(1, "TryAdd keeps repeated registration idempotent");
    }

    [Test]
    public void AddPgSqlRepositoryProvider_ForASecondContextTypeWithMatchingOptions_Succeeds()
    {
        // Arrange
        var dbName = $"test_di_{++_databaseCounter}";

        var services = new ServiceCollection();

        AddInfrastructure(services, dbName);

        // Act
        var act = () => services.AddPgSqlRepositoryProvider<SecondTestDbContext, SecondTestDatabaseContextProvider>(
            options =>
            {
                options.DefaultDbName = dbName;
                options.MaxPoolSize = MaxPoolSizePerDatabase;
            });

        // Assert
        act.Should().NotThrow();

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IDatabaseContextProvider<TestDbContext>>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IDatabaseContextProvider<SecondTestDbContext>>().Should().NotBeNull();
    }

    /// <remarks>
    /// The options are one process-wide instance which every configure action is applied to, so two differing
    /// registrations would otherwise merge per property into a combination neither caller asked for -- including
    /// the default database name, which decides what the provider connects to.
    /// </remarks>
    [Test]
    public void AddPgSqlRepositoryProvider_ForASecondContextTypeWithDifferentOptions_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        AddInfrastructure(services, $"test_di_{++_databaseCounter}");

        // Act
        var act = () => services.AddPgSqlRepositoryProvider<SecondTestDbContext, SecondTestDatabaseContextProvider>(
            options => options.DefaultDbName = "a_completely_different_database");

        // Assert
        var assertion = act.Should().Throw<InvalidOperationException>();

        assertion.WithMessage("*a_completely_different_database*");
    }

    private static ServiceProvider BuildServiceProvider(string dbName)
    {
        var services = new ServiceCollection();

        AddInfrastructure(services, dbName);

        // Singleton, matching the lifetime the library documents for its repositories.
        services.AddSingleton<IEntityRepository<long, TestEntity>, TestEntityRepository>();

        return services.BuildServiceProvider();
    }

    private static void AddInfrastructure(IServiceCollection services, string dbName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = PostgreSqlFixture.BaseConnectionString
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        services.AddPgSqlRepositoryProvider<TestDbContext, TestDatabaseContextProvider>(options =>
        {
            options.DefaultDbName = dbName;
            options.MaxPoolSize = MaxPoolSizePerDatabase;
        });
    }
}
