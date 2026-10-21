using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class TemporalRepositoryTests : IntegrationTestBase
{
    [Test]
    public async Task GetLatestValidAsync_PeriodCoveringNow_ReturnsIt()
    {
        // Arrange
        await SeedAsync("current", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().NotBeNull();
        valid.Name.Should().Be("current");
    }

    [Test]
    public async Task GetLatestValidAsync_PeriodWithoutEnd_TreatsItAsStillOpen()
    {
        // Arrange
        await SeedAsync("open ended", DateTime.UtcNow.AddDays(-1), null);

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().NotBeNull();
        valid.Name.Should().Be("open ended");
    }

    [Test]
    public async Task GetLatestValidAsync_PeriodAlreadyEnded_DoesNotReturnIt()
    {
        // Arrange
        await SeedAsync("expired", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-5));

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().BeNull();
    }

    [Test]
    public async Task GetLatestValidAsync_PeriodNotYetStarted_DoesNotReturnIt()
    {
        // Arrange
        await SeedAsync("future", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(10));

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().BeNull();
    }

    /// <remarks>
    /// A start date is required. An entity without one is never valid, rather than being valid from the beginning
    /// of time as the symmetry with an unset end date might suggest.
    /// </remarks>
    [Test]
    public async Task GetLatestValidAsync_PeriodWithoutStart_DoesNotReturnIt()
    {
        // Arrange
        await SeedAsync("no start", null, DateTime.UtcNow.AddDays(1));

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().BeNull();
    }

    [Test]
    public async Task GetLatestValidAsync_SeveralValidPeriods_ReturnsTheOneStartingLatest()
    {
        // Arrange
        await SeedAsync("older", DateTime.UtcNow.AddDays(-10), null);
        await SeedAsync("newer", DateTime.UtcNow.AddDays(-1), null);
        await SeedAsync("oldest", DateTime.UtcNow.AddDays(-20), null);

        // Act
        var valid = (await TemporalRepository.GetLatestValidAsync()).ShouldExist();

        // Assert
        valid.Name.Should().Be("newer");
    }

    /// <remarks>
    /// Comparison is at full timestamp precision rather than truncated to a date, so a period beginning later
    /// today is not yet valid.
    /// </remarks>
    [Test]
    public async Task GetLatestValidAsync_PeriodStartingLaterToday_IsNotYetValid()
    {
        // Arrange
        await SeedAsync("later today", DateTime.UtcNow.AddHours(2), null);

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync();

        // Assert
        valid.Should().BeNull();
    }

    [Test]
    public async Task GetLatestValidAsync_WithPredicate_AppliesBothTheValidityRuleAndThePredicate()
    {
        // Arrange
        await SeedAsync("wanted", DateTime.UtcNow.AddDays(-1), null);
        await SeedAsync("unwanted", DateTime.UtcNow.AddHours(-1), null);

        // Act
        var valid = await TemporalRepository.GetLatestValidAsync(e => e.Name == "wanted");

        // Assert
        valid.Should().NotBeNull();
        valid.Name.Should().Be("wanted");
    }

    /// <remarks>
    /// The dates map to <c>timestamp with time zone</c>, which cannot accept a value whose kind is not UTC. This
    /// pins the requirement stated on the entity so it cannot regress silently into a mis-stored local time.
    /// </remarks>
    [Test]
    public async Task AddAsync_NonUtcDate_IsRejected()
    {
        // Arrange
        var unspecified = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var act = async () => await TemporalRepository.AddAsync(
            new TestTemporalEntity { Name = "bad kind", DateBegin = unspecified });

        // Assert
        var assertion = await act.Should().ThrowAsync<DbUpdateException>();

        assertion.WithInnerException<ArgumentException>().WithMessage("*only UTC is supported*");
    }

    private async Task SeedAsync(string name, DateTime? dateBegin, DateTime? dateEnd)
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
