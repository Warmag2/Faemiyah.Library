using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Models;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class ConcurrencyTests : IntegrationTestBase
{
    /// <summary>
    /// Locates the single entity these tests operate on.
    /// </summary>
    /// <remarks>
    /// Matches on the value rather than the name, because the name is what the tests overwrite and a predicate which
    /// stopped matching after the first update would make the second one look like a missing entity instead of the
    /// conflict it is meant to be.
    /// </remarks>
    private static Expression<Func<TestEntity, bool>> TheEntity => e => e.Value == 1;

    /// <remarks>
    /// The token maps to the PostgreSQL <c>xmin</c> system column, so a populated value proves both that the
    /// mapping took effect and that EF reads the generated value back after the insert.
    /// </remarks>
    [Test]
    public async Task AddAsync_PopulatesConcurrencyTokenFromTheDatabase()
    {
        // Act
        var added = (await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Assert
        added.ConcurrencyToken.Should().NotBe(0);
    }

    [Test]
    public async Task GetAsync_ReadsBackTheConcurrencyToken()
    {
        // Arrange
        var added = (await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        var fetched = (await Repository.GetAsync(TheEntity)).ShouldExist();

        // Assert
        fetched.ConcurrencyToken.Should().Be(added.ConcurrencyToken);
    }

    [Test]
    public async Task UpdateAsync_AdvancesTheConcurrencyToken()
    {
        // Arrange
        var added = (await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 }))
            .ShouldSucceedAs(OperationStatus.Added);
        var original = added.ConcurrencyToken;

        // Act
        var fetched = (await Repository.GetAsync(TheEntity)).ShouldExist();

        fetched.Name = "second";

        var updated = await Repository.UpdateAsync(fetched, TheEntity);

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var reread = (await Repository.GetAsync(TheEntity)).ShouldExist();

        reread.ConcurrencyToken.Should().NotBe(original);
        reread.Name.Should().Be("second");
    }

    /// <remarks>
    /// The lost update this exists to prevent: two callers read the same revision, the first writes, and the second
    /// must be refused rather than silently overwriting.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_WithStaleToken_ReturnsConflict()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var readByFirstCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();
        var readBySecondCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();

        readBySecondCaller.Name = "written by second";

        (await Repository.UpdateAsync(readBySecondCaller, TheEntity)).ShouldSucceedAs(OperationStatus.Updated);

        // Act
        readByFirstCaller.Name = "written by first";

        var result = await Repository.UpdateAsync(readByFirstCaller, TheEntity);

        // Assert
        result.ShouldFailAs(OperationStatus.Conflict);

        var stored = (await Repository.GetAsync(TheEntity)).ShouldExist();

        stored.Name.Should().Be("written by second");
    }

    [Test]
    public async Task UpdateAsync_WithCurrentToken_Succeeds()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var fetched = (await Repository.GetAsync(TheEntity)).ShouldExist();

        // Act
        fetched.Name = "second";

        var updated = (await Repository.UpdateAsync(fetched, TheEntity)).ShouldSucceedAs(OperationStatus.Updated);

        // Assert
        updated.Name.Should().Be("second");
    }

    /// <remarks>
    /// A caller which built the entity rather than reading it has no revision to offer, so the repository adopts the
    /// stored one. The update goes through -- it is checked against the state as of the repository's own read, which
    /// a competing writer from before that read cannot fail.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_WithoutAToken_AdoptsTheStoredRevisionAndSucceeds()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var readByFirstCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();
        var readBySecondCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();

        readBySecondCaller.Name = "written by second";

        await Repository.UpdateAsync(readBySecondCaller, TheEntity);

        // Act
        readByFirstCaller.Name = "written by first";
        readByFirstCaller.ConcurrencyToken = default;

        var updated = await Repository.UpdateAsync(readByFirstCaller, TheEntity);

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);
        readByFirstCaller.ConcurrencyToken.Should().NotBe(0, "the stored revision was adopted onto the entity");

        var stored = (await Repository.GetAsync(TheEntity)).ShouldExist();

        stored.Name.Should().Be("written by first");
    }

    [Test]
    public async Task AddOrUpdateAsync_WithStaleToken_ReturnsConflict()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var readByFirstCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();
        var readBySecondCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();

        readBySecondCaller.Name = "written by second";

        (await Repository.UpdateAsync(readBySecondCaller, TheEntity)).ShouldSucceedAs(OperationStatus.Updated);

        // Act
        readByFirstCaller.Name = "written by first";

        var result = await Repository.AddOrUpdateAsync(readByFirstCaller, TheEntity);

        // Assert
        result.ShouldFailAs(OperationStatus.Conflict);

        var stored = (await Repository.GetAsync(TheEntity)).ShouldExist();

        stored.Name.Should().Be("written by second");
        (await Repository.GetAllAsync()).Should().HaveCount(1, "a conflict never falls back to an insert");
    }

    /// <remarks>
    /// A failed save is not retried by the next save on the same context, because the change tracker is cleared on
    /// failure. A derived repository composing several operations onto one context depends on that.
    /// </remarks>
    [Test]
    public async Task SaveWithContextAsync_AfterAConflict_LeavesTheContextUsable()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var readByFirstCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();
        var readBySecondCaller = (await Repository.GetAsync(TheEntity)).ShouldExist();

        readBySecondCaller.Name = "written by second";

        (await Repository.UpdateAsync(readBySecondCaller, TheEntity)).ShouldSucceedAs(OperationStatus.Updated);

        readByFirstCaller.Name = "written by first";

        await using var dbContext = await ContextProvider.GetDbContextAsync();

        // Act
        var conflicted = await Repository.UpdateUsingContextAsync(dbContext, readByFirstCaller, TheEntity);
        var added = await Repository.AddUsingContextAsync(dbContext, new TestEntity { Name = "afterwards", Value = 2 });

        // Assert
        conflicted.ShouldFailAs(OperationStatus.Conflict);
        added.ShouldSucceedAs(OperationStatus.Added);
        dbContext.ChangeTracker.Entries().Should().OnlyContain(entry => entry.Entity == added.Value);

        var stored = (await Repository.GetAsync(TheEntity)).ShouldExist();

        stored.Name.Should().Be("written by second", "the failed update was not retried by the later save");
        (await Repository.GetAllAsync()).Should().HaveCount(2);
    }
}
