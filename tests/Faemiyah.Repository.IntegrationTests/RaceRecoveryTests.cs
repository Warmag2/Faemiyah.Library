using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

/// <summary>
/// Covers what happens when two callers try to add the same entity at once.
/// </summary>
/// <remarks>
/// The pre-existence check and the insert are separate statements, so a duplicate can appear between them. The
/// library is expected to translate the resulting constraint violation into the documented return value rather than
/// letting a provider exception escape.
/// </remarks>
[TestFixture]
public class RaceRecoveryTests : IntegrationTestBase
{
    /// <summary>
    /// How many callers contend for the same unique ID at once.
    /// </summary>
    private const int ConcurrentCallers = 16;

    /// <summary>
    /// How many rounds to allow for the insert level race to occur.
    /// </summary>
    /// <remarks>
    /// Whether the losers are caught by the pre-existence check or by the constraint violation is down to timing, so
    /// a single round could legitimately never reach the recovery path. Retrying makes the test dependable without
    /// weakening what it asserts, since the contract is checked on every round regardless.
    /// </remarks>
    private const int RaceAttempts = 12;

    /// <summary>
    /// A pool large enough for the callers to overlap rather than queue for connections.
    /// </summary>
    private const int PoolSizeForContention = 24;

    private static int _databaseCounter;

    /// <remarks>
    /// The classifier decides whether a failed save is a lost race or a real fault, and it does so by reading a
    /// provider specific error code. Nothing but a genuine failure proves it reads the right one.
    /// </remarks>
    [Test]
    public async Task IsUniqueConstraintViolation_GenuineDuplicateKeyFailure_IsRecognised()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        (await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);

        await using var dbContext = await ContextProvider.GetDbContextAsync();

        dbContext.TestUuidEntities.Add(new TestUuidEntity { UniqueId = uniqueId, Name = "second" });

        // Act
        var act = async () => await dbContext.SaveChangesAsync();

        var assertion = await act.Should().ThrowAsync<DbUpdateException>();

        // Assert
        CreateObservingRepository().ClassifyWithoutCounting(assertion.Which).Should().BeTrue();
    }

    /// <remarks>
    /// The other half of the classifier's job. Accepting any failed save would turn genuine faults into a silent
    /// <see cref="OperationStatus.Duplicate"/>, which is worse than the exception it was meant to replace.
    /// </remarks>
    [Test]
    public async Task IsUniqueConstraintViolation_UnrelatedFailure_IsNotRecognised()
    {
        // Arrange
        await using var dbContext = await ContextProvider.GetDbContextAsync();

        // A child pointing at a parent which does not exist breaks the foreign key, not a unique index.
        dbContext.TestChildEntities.Add(new TestChildEntity { Label = "orphan", TestEntityId = 987654321 });

        // Act
        var act = async () => await dbContext.SaveChangesAsync();

        var assertion = await act.Should().ThrowAsync<DbUpdateException>();

        // Assert
        CreateObservingRepository().ClassifyWithoutCounting(assertion.Which).Should().BeFalse();
    }

    [Test]
    public async Task AddAsync_ConcurrentDuplicates_YieldOneWinnerAndReachTheRecoveryPath()
    {
        // Arrange
        var repository = CreateObservingRepository(PoolSizeForContention);

        // Act
        for (var attempt = 0; attempt < RaceAttempts && repository.UniqueViolationsHandled == 0; attempt++)
        {
            var uniqueId = Guid.NewGuid();

            var results = await Task.WhenAll(
                Enumerable.Range(0, ConcurrentCallers).Select(caller =>
                    repository.AddAsync(
                        new TestUuidEntity { UniqueId = uniqueId, Name = $"caller-{caller}" },
                        e => e.UniqueId == uniqueId)));

            // Assert -- true on every round, whether the losers were stopped by the pre-existence check or by the
            // constraint violation. No caller may see an exception, exactly one may add, every other one must be
            // told it is a duplicate, and exactly one row may exist.
            results.Count(result => result.Status == OperationStatus.Added).Should().Be(1);
            results.Count(result => result.Status == OperationStatus.Duplicate).Should().Be(ConcurrentCallers - 1);
            (await repository.GetAllAsync(e => e.UniqueId == uniqueId)).Should().HaveCount(1);
        }

        // Assert
        repository.UniqueViolationsHandled.Should().BeGreaterThan(
            0,
            "the insert level race is expected to occur within {0} rounds of {1} concurrent callers",
            RaceAttempts,
            ConcurrentCallers);
    }

    /// <remarks>
    /// The loser of a race has to re-read and hand back the winner's entity. It can do so reliably because the
    /// violation is only raised once the winning transaction has committed, so the row is already visible.
    /// </remarks>
    [Test]
    public async Task GetOrAddAsync_ConcurrentCallers_AllReceiveAnEntityAndOnlyOneCreatesIt()
    {
        // Arrange
        var repository = CreateObservingRepository(PoolSizeForContention);
        var uniqueId = Guid.NewGuid();

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentCallers).Select(_ =>
                repository.GetOrAddAsync(
                    new TestUuidEntity { UniqueId = uniqueId, Name = "contender" },
                    e => e.UniqueId == uniqueId)));

        // Assert
        results.Should().AllSatisfy(result => result.IsSuccess.Should().BeTrue());
        results.Count(result => result.Status == OperationStatus.Added)
            .Should().Be(1, "exactly one caller creates the entity");
        results.Count(result => result.Status == OperationStatus.Found)
            .Should().Be(ConcurrentCallers - 1, "every other caller receives the winner's entity");
        (await repository.GetAllAsync(e => e.UniqueId == uniqueId)).Should().HaveCount(1);
    }

    /// <remarks>
    /// Concurrent upserts of one entity are expected to produce concurrency conflicts, not to be shielded from them:
    /// each losing caller reads the row and then overwrites it, so a rival committing in between is a genuine
    /// conflict and reporting it is correct. What must hold is that exactly one caller creates the entity, that every
    /// other one reports an update or a conflict rather than throwing, and that one row exists at the end.
    /// </remarks>
    [Test]
    public async Task AddOrUpdateAsync_ConcurrentCallers_ConvergeOnOneRowAndReportOnlyUpdatesOrConflicts()
    {
        // Arrange
        var repository = CreateObservingRepository(PoolSizeForContention);
        var uniqueId = Guid.NewGuid();

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentCallers).Select(caller =>
                repository.AddOrUpdateAsync(
                    new TestUuidEntity { UniqueId = uniqueId, Name = $"caller-{caller}" },
                    e => e.UniqueId == uniqueId)));

        // Assert
        results.Count(result => result.Status == OperationStatus.Added)
            .Should().Be(1, "only one caller can create the entity");
        results.Should().OnlyContain(result =>
            result.Status == OperationStatus.Added
            || result.Status == OperationStatus.Updated
            || result.Status == OperationStatus.Conflict);
        (await repository.GetAllAsync(e => e.UniqueId == uniqueId)).Should().HaveCount(1);
    }

    private static RaceObservingRepository CreateObservingRepository(int maxPoolSize = 4)
    {
        var provider = CreateProvider($"test_race_{Interlocked.Increment(ref _databaseCounter)}", maxPoolSize);

        return new RaceObservingRepository(
            NullLogger<RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>>.Instance,
            provider);
    }
}
