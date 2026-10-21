using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class TimeProviderTests : IntegrationTestBase
{
    /// <summary>
    /// An instant far enough from any real clock reading that a test asserting on it cannot pass by accident.
    /// </summary>
    private static readonly DateTime FixedNow = new(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task AddAsync_TakesUpdateTimeFromTheSuppliedClock()
    {
        // Arrange
        var repository = CreateEntityRepository(new FixedTimeProvider(FixedNow));

        // Act
        var added = (await repository.AddAsync(new TestEntity { Name = "first" })).ShouldSucceedAs(OperationStatus.Added);

        // Assert
        added.UpdateTime.Should().Be(FixedNow);
    }

    [Test]
    public async Task UpdateAsync_TakesUpdateTimeFromTheSuppliedClock()
    {
        // Arrange
        var clock = new FixedTimeProvider(FixedNow);
        var repository = CreateEntityRepository(clock);

        var added = (await repository.AddAsync(new TestEntity { Name = "first", Value = 1 }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        clock.Advance(TimeSpan.FromDays(1));

        added.Name = "second";

        // The predicate locates the stored row, which still carries the original name, so it matches on the value
        // the update leaves alone.
        var updated = (await repository.UpdateAsync(added, e => e.Value == 1)).ShouldSucceedAs(OperationStatus.Updated);

        // Assert
        updated.UpdateTime.Should().Be(FixedNow.AddDays(1));
    }

    /// <remarks>
    /// The whole reason the clock is injectable: validity is evaluated against it, so a period which the system
    /// clock would never see as current can still be tested.
    /// </remarks>
    [Test]
    public async Task GetLatestValidAsync_EvaluatesValidityAgainstTheSuppliedClock()
    {
        // Arrange
        await SeedTemporalAsync("valid in 2030", FixedNow.AddDays(-1), FixedNow.AddDays(1));

        var atFixedNow = CreateTemporalRepository(new FixedTimeProvider(FixedNow));
        var yearsEarlier = CreateTemporalRepository(new FixedTimeProvider(FixedNow.AddYears(-5)));

        // Act
        var foundNow = await atFixedNow.GetLatestValidAsync();
        var foundEarlier = await yearsEarlier.GetLatestValidAsync();

        // Assert
        foundNow.ShouldExist().Name.Should().Be("valid in 2030");
        foundEarlier.Should().BeNull("the period had not started at that point");
    }

    [Test]
    public async Task GetLatestValidAtAsync_AnswersForAnArbitraryInstant()
    {
        // Arrange
        await SeedTemporalAsync("first period", FixedNow.AddDays(-10), FixedNow.AddDays(-5));
        await SeedTemporalAsync("second period", FixedNow.AddDays(-4), FixedNow.AddDays(4));

        // Act
        var duringFirst = await TemporalRepository.GetLatestValidAtAsync(FixedNow.AddDays(-7));
        var duringSecond = await TemporalRepository.GetLatestValidAtAsync(FixedNow);
        var betweenThem = await TemporalRepository.GetLatestValidAtAsync(FixedNow.AddDays(-4.5));

        // Assert
        duringFirst.ShouldExist().Name.Should().Be("first period");
        duringSecond.ShouldExist().Name.Should().Be("second period");
        betweenThem.Should().BeNull("neither period covers the gap");
    }

    /// <remarks>
    /// The boundaries are inclusive, so the first and last instant of a period are both covered.
    /// </remarks>
    [Test]
    public async Task GetLatestValidAtAsync_AtTheExactBoundaries_IsValid()
    {
        // Arrange
        var begin = FixedNow;
        var end = FixedNow.AddDays(1);

        await SeedTemporalAsync("bounded", begin, end);

        // Act
        var atBegin = await TemporalRepository.GetLatestValidAtAsync(begin);
        var atEnd = await TemporalRepository.GetLatestValidAtAsync(end);
        var justBefore = await TemporalRepository.GetLatestValidAtAsync(begin.AddSeconds(-1));
        var justAfter = await TemporalRepository.GetLatestValidAtAsync(end.AddSeconds(1));

        // Assert
        atBegin.Should().NotBeNull();
        atEnd.Should().NotBeNull();
        justBefore.Should().BeNull();
        justAfter.Should().BeNull();
    }

    [Test]
    public async Task GetLatestValidAtAsync_NonUtcInstant_Throws()
    {
        // Arrange
        var unspecified = new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var act = async () => await TemporalRepository.GetLatestValidAtAsync(unspecified);

        // Assert
        var assertion = await act.Should().ThrowAsync<ArgumentException>();

        assertion.WithMessage("*UTC*");
    }

    private TestEntityRepository CreateEntityRepository(TimeProvider timeProvider)
    {
        return new TestEntityRepository(
            NullLogger<RepositoryBase<TestDbContext, long, TestEntity>>.Instance,
            ContextProvider,
            timeProvider);
    }

    private TestTemporalEntityRepository CreateTemporalRepository(TimeProvider timeProvider)
    {
        return new TestTemporalEntityRepository(
            NullLogger<TemporalRepositoryBase<TestDbContext, long, TestTemporalEntity>>.Instance,
            ContextProvider,
            timeProvider);
    }

    private async Task SeedTemporalAsync(string name, DateTime? dateBegin, DateTime? dateEnd)
    {
        await using var dbContext = await ContextProvider.GetDbContextAsync();

        dbContext.TestTemporalEntities.Add(new TestTemporalEntity
        {
            Name = name,
            DateBegin = dateBegin,
            DateEnd = dateEnd
        });

        await dbContext.SaveChangesAsync();
    }
}
