using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class ProvisioningTests : IntegrationTestBase
{
    /// <summary>
    /// The number of concurrent callers used to prove provisioning is not merely serialised, but performed once.
    /// </summary>
    private const int ConcurrentCallers = 20;

    [Test]
    public async Task GetDbContextAsync_UnknownDatabase_CreatesAndProvisionsIt()
    {
        // Act
        await using var dbContext = await ContextProvider.GetDbContextAsync();

        // Assert
        var canQuery = await dbContext.TestEntities.AnyAsync();

        canQuery.Should().BeFalse();
        ContextProvider.ProvisioningCount.Should().Be(1);
    }

    [Test]
    public async Task GetDbContextAsync_CalledRepeatedly_ProvisionsOnlyOnce()
    {
        // Act
        for (var i = 0; i < 3; i++)
        {
            await using var dbContext = await ContextProvider.GetDbContextAsync();
        }

        // Assert
        ContextProvider.ProvisioningCount.Should().Be(1);
    }

    /// <remarks>
    /// This is the direct test of the <c>Lazy{Task}</c> gate. A lock alone would serialise these callers and still
    /// provision once per call; only running the delegate once gives a count of one.
    /// </remarks>
    [Test]
    public async Task GetDbContextAsync_ConcurrentCallers_ProvisionsOnlyOnce()
    {
        // Act
        var contexts = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentCallers).Select(_ => ContextProvider.GetDbContextAsync()));

        // Assert
        ContextProvider.ProvisioningCount.Should().Be(1);
        contexts.Should().AllSatisfy(context => context.Should().NotBeNull());

        foreach (var context in contexts)
        {
            await context.DisposeAsync();
        }
    }

    /// <remarks>
    /// A faulted task left in the map would make the database permanently unusable for the process, so the failure
    /// has to be evicted. The second call must therefore try again and succeed.
    /// </remarks>
    [Test]
    public async Task GetDbContextAsync_AfterFailedProvisioning_RetriesInsteadOfCachingTheFailure()
    {
        // Arrange
        ContextProvider.FailNextProvisioning = true;

        // Act
        var firstAttempt = async () => await ContextProvider.GetDbContextAsync();

        await firstAttempt.Should().ThrowAsync<TestProvisioningException>();

        await using var dbContext = await ContextProvider.GetDbContextAsync();

        // Assert
        ContextProvider.ProvisioningCount.Should().Be(2);

        var canQuery = await dbContext.TestEntities.AnyAsync();

        canQuery.Should().BeFalse();
    }

    [Test]
    public async Task GetDbContextAsync_NoNameSuppliedAndNoDefaultConfigured_Throws()
    {
        // Arrange
        var provider = CreateProvider(null);

        // Act
        var act = async () => await provider.GetDbContextAsync();

        // Assert
        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();

        assertion.WithMessage("*DefaultDbName*");
    }

    [Test]
    public async Task GetDbContextAsync_ExplicitDatabaseName_UsesThatDatabase()
    {
        // Arrange
        var provider = CreateProvider(null);
        var explicitName = "test_explicit_name";

        // Act
        await using var dbContext = await provider.GetDbContextAsync(explicitName);

        // Assert
        dbContext.Database.GetDbConnection().Database.Should().Be(explicitName);
        provider.ProvisioningCount.Should().Be(1);
    }
}
