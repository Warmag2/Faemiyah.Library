using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Faemiyah.Repository.Models;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

/// <summary>
/// Covers the repository base, which identifies entities purely by the predicates the caller supplies.
/// </summary>
/// <remarks>
/// The entity under test deliberately has no public identifier, so nothing here may depend on one. Everything to do
/// with unique IDs lives in <see cref="RepositoryBaseWithUuidTests"/>.
/// </remarks>
[TestFixture]
public class RepositoryBaseTests : IntegrationTestBase
{
    [Test]
    public async Task AddAsync_ThenGetAsync_RoundTripsTheEntity()
    {
        // Act
        var added = (await Repository.AddAsync(new TestEntity { Name = "first", Value = 42 }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Assert
        added.Id.Should().NotBe(0, "the database generates the internal key");

        var fetched = (await Repository.GetAsync(e => e.Name == "first")).ShouldExist();

        fetched.Name.Should().Be("first");
        fetched.Value.Should().Be(42);
        fetched.Id.Should().Be(added.Id);
    }

    [Test]
    public async Task AddAsync_SetsUpdateTime()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var added = (await Repository.AddAsync(new TestEntity { Name = "first" })).ShouldSucceedAs(OperationStatus.Added);

        // Assert
        added.UpdateTime.Should().BeAfter(before);
    }

    [Test]
    public async Task AddAsync_WhenTheDuplicationPredicateMatches_ReturnsDuplicate()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first" });

        // Act
        var second = await Repository.AddAsync(
            new TestEntity { Name = "first", Value = 2 },
            e => e.Name == "first");

        // Assert
        second.ShouldFailAs(OperationStatus.Duplicate);
        (await Repository.GetAllAsync()).Should().HaveCount(1);
    }

    /// <remarks>
    /// The check is opt-in, so leaving the predicate out has to add unconditionally rather than guess at what makes
    /// two entities the same.
    /// </remarks>
    [Test]
    public async Task AddAsync_WithoutADuplicationPredicate_AddsUnconditionally()
    {
        // Act
        var first = (await Repository.AddAsync(new TestEntity { Name = "same" })).ShouldSucceedAs(OperationStatus.Added);
        var second = (await Repository.AddAsync(new TestEntity { Name = "same" })).ShouldSucceedAs(OperationStatus.Added);

        // Assert
        second.Id.Should().NotBe(first.Id);
        (await Repository.GetAllAsync()).Should().HaveCount(2);
    }

    [Test]
    public async Task GetAsync_PredicateMatchingNothing_ReturnsNull()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first" });

        // Act
        var fetched = await Repository.GetAsync(e => e.Name == "absent");

        // Assert
        fetched.Should().BeNull();
    }

    [Test]
    public async Task GetAsync_PredicateMatchingSeveralEntities_Throws()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "shared" });
        await Repository.AddAsync(new TestEntity { Name = "shared" });

        // Act
        var act = async () => await Repository.GetAsync(e => e.Name == "shared");

        // Assert
        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();

        assertion.WithMessage("*more than one*");
    }

    [Test]
    public async Task GetAsync_NullPredicate_Throws()
    {
        // Act
        var act = async () => await Repository.GetAsync(predicate: null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task UpdateAsync_ExistingEntity_ReplacesScalarValues()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        var fetched = (await Repository.GetAsync(e => e.Name == "first")).ShouldExist();

        // Act
        fetched.Name = "second";
        fetched.Value = 2;

        var updated = await Repository.UpdateAsync(fetched, e => e.Name == "first");

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var stored = (await Repository.GetAsync(e => e.Name == "second")).ShouldExist();

        stored.Value.Should().Be(2);
    }

    /// <remarks>
    /// Callers build entities from payloads which carry no internal key, so the update has to locate the row by the
    /// supplied predicate regardless of what the entity's own key is set to.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_EntityWithoutInternalKey_StillUpdates()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 7 });

        // Act
        var detached = new TestEntity { Name = "rebuilt from a payload", Value = 7 };
        var updated = await Repository.UpdateAsync(detached, e => e.Value == 7);

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var stored = (await Repository.GetAsync(e => e.Value == 7)).ShouldExist();

        stored.Name.Should().Be("rebuilt from a payload");
        (await Repository.GetAllAsync()).Should().HaveCount(1);
    }

    [Test]
    public async Task UpdateAsync_UnknownEntity_ReturnsNotFound()
    {
        // Act
        var updated = await Repository.UpdateAsync(
            new TestEntity { Name = "absent" },
            e => e.Name == "absent");

        // Assert
        updated.ShouldFailAs(OperationStatus.NotFound);
        (await Repository.GetAllAsync()).Should().BeEmpty("nothing was added in its place");
    }

    [Test]
    public async Task DeleteAsync_ByPredicate_RemovesOnlyMatchingEntities()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "keep", Value = 1 });
        await Repository.AddAsync(new TestEntity { Name = "drop", Value = 2 });
        await Repository.AddAsync(new TestEntity { Name = "drop", Value = 3 });

        // Act
        var deleted = await Repository.DeleteAsync(e => e.Name == "drop");

        // Assert
        deleted.ShouldSucceedAs(OperationStatus.Deleted).Should().HaveCount(2);

        var remaining = await Repository.GetAllAsync();

        remaining.Should().HaveCount(1);
        remaining.Single().Name.Should().Be("keep");
    }

    /// <remarks>
    /// Deleting nothing is still a successful delete, so it reports success with an empty set rather than a failure.
    /// </remarks>
    [Test]
    public async Task DeleteAsync_ByPredicateMatchingNothing_ReturnsDeletedWithEmptySet()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "keep" });

        // Act
        var deleted = await Repository.DeleteAsync(e => e.Name == "absent");

        // Assert
        deleted.ShouldSucceedAs(OperationStatus.Deleted).Should().BeEmpty();
    }

    [Test]
    public async Task GetOrAddAsync_WhenAbsent_ReturnsAdded()
    {
        // Act
        var result = await Repository.GetOrAddAsync(
            new TestEntity { Name = "first" },
            e => e.Name == "first");

        // Assert
        result.ShouldSucceedAs(OperationStatus.Added).Id.Should().NotBe(0);
    }

    [Test]
    public async Task GetOrAddAsync_WhenPresent_ReturnsFoundWithTheExistingEntity()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        // Act
        var result = await Repository.GetOrAddAsync(
            new TestEntity { Name = "first", Value = 2 },
            e => e.Name == "first");

        // Assert
        result.ShouldSucceedAs(OperationStatus.Found).Value
            .Should().Be(1, "the stored entity is returned rather than the supplied one");
    }

    /// <remarks>
    /// Calling twice with the same entity and predicate must be idempotent. It would not be if the entity which gets
    /// inserted did not itself satisfy the predicate used to look it up.
    /// </remarks>
    [Test]
    public async Task GetOrAddAsync_CalledTwice_AddsOnlyOnce()
    {
        // Arrange
        var entity = new TestEntity { Name = "first" };

        // Act
        var first = await Repository.GetOrAddAsync(entity, e => e.Name == "first");
        var second = await Repository.GetOrAddAsync(entity, e => e.Name == "first");

        // Assert
        first.Status.Should().Be(OperationStatus.Added);
        second.Status.Should().Be(OperationStatus.Found);
        (await Repository.GetAllAsync()).Should().HaveCount(1);
    }

    [Test]
    public async Task GetOrAddAsync_NullPredicate_Throws()
    {
        // Act
        var act = async () => await Repository.GetOrAddAsync(new TestEntity { Name = "first" }, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task AddOrUpdateAsync_WhenAbsent_ReturnsAdded()
    {
        // Act
        var result = await Repository.AddOrUpdateAsync(
            new TestEntity { Name = "first" },
            e => e.Name == "first");

        // Assert
        result.ShouldSucceedAs(OperationStatus.Added).Id.Should().NotBe(0);
    }

    /// <remarks>
    /// The update branch used to be unreachable, because it located the row by the internal key which a caller
    /// building an entity from a payload never has.
    /// </remarks>
    [Test]
    public async Task AddOrUpdateAsync_WhenPresent_ReturnsUpdated()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first", Value = 1 });

        // Act
        var result = await Repository.AddOrUpdateAsync(
            new TestEntity { Name = "first", Value = 2 },
            e => e.Name == "first");

        // Assert
        result.ShouldSucceedAs(OperationStatus.Updated).Value.Should().Be(2);

        var stored = (await Repository.GetAsync(e => e.Name == "first")).ShouldExist();

        stored.Value.Should().Be(2);
        (await Repository.GetAllAsync()).Should().HaveCount(1);
    }

    [Test]
    public async Task UpdateAsync_NullPredicate_Throws()
    {
        // Act
        var act = async () => await Repository.UpdateAsync(new TestEntity { Name = "first" }, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <remarks>
    /// An over-broad predicate on a write means the wrong row was about to be overwritten, so it has to be reported
    /// as clearly as it is on the read paths rather than as a bare sequence error from the query provider.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_PredicateMatchingSeveralEntities_Throws()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "shared", Value = 1 });
        await Repository.AddAsync(new TestEntity { Name = "shared", Value = 2 });

        // Act
        var act = async () => await Repository.UpdateAsync(
            new TestEntity { Name = "replacement" },
            e => e.Name == "shared");

        // Assert
        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();

        assertion.WithMessage("*more than one*");

        (await Repository.GetAllAsync(e => e.Name == "shared")).Should().HaveCount(2, "nothing was overwritten");
    }

    /// <remarks>
    /// The predicate is what makes this an upsert rather than an insert, so a missing one has to be refused. Letting
    /// it through would fall back on the optional duplicate check of the add path and silently add a second row.
    /// </remarks>
    [Test]
    public async Task AddOrUpdateAsync_NullPredicate_Throws()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "first" });

        // Act
        var act = async () => await Repository.AddOrUpdateAsync(new TestEntity { Name = "first" }, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        (await Repository.GetAllAsync()).Should().HaveCount(1, "no duplicate was inserted");
    }

    [Test]
    public async Task GetAllAsync_WithPredicate_ReturnsOnlyMatching()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity { Name = "a", Value = 1 });
        await Repository.AddAsync(new TestEntity { Name = "b", Value = 2 });

        // Act
        var matching = await Repository.GetAllAsync(e => e.Value > 1);

        // Assert
        matching.Should().HaveCount(1);
        matching.Single().Name.Should().Be("b");
    }

    [Test]
    public async Task GetAllAsync_EmptyTable_ReturnsEmpty()
    {
        // Act
        var all = await Repository.GetAllAsync();

        // Assert
        all.Should().BeEmpty();
    }

    [Test]
    public async Task GetAsync_WithProcessIncludes_LoadsRelatedEntities()
    {
        // Arrange
        await Repository.AddAsync(new TestEntity
        {
            Name = "parent",
            Children = [new TestChildEntity { Label = "one" }, new TestChildEntity { Label = "two" }]
        });

        // Act
        var withIncludes = (await Repository.GetAsync(e => e.Name == "parent", processIncludes: true)).ShouldExist();
        var withoutIncludes = (await Repository.GetAsync(e => e.Name == "parent")).ShouldExist();

        // Assert
        withIncludes.Children.Should().HaveCount(2);
        withoutIncludes.Children.Should().BeNullOrEmpty();
    }
}
