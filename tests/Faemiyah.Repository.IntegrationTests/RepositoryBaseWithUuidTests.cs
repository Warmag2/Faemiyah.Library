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

/// <summary>
/// Covers what the unique identifier adds on top of the plain repository base.
/// </summary>
/// <remarks>
/// The public identifier now lives on its own entity and repository base, so everything which depends on one is
/// gathered here rather than spread through <see cref="RepositoryBaseTests"/>.
/// </remarks>
[TestFixture]
public class RepositoryBaseWithUuidTests : IntegrationTestBase
{
    [Test]
    public async Task AddAsync_WithoutSettingUniqueId_StillGetsOne()
    {
        // Act
        var added = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Assert
        added.UniqueId.Should().NotBe(Guid.Empty);
    }

    /// <remarks>
    /// Two entities with no unique ID assigned must not collide, which they would if the default were
    /// <see cref="Guid.Empty"/> rather than a fresh value per instance.
    /// </remarks>
    [Test]
    public async Task AddAsync_TwoEntitiesWithDefaultUniqueIds_BothSucceed()
    {
        // Act
        var first = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);
        var second = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "second" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Assert
        second.UniqueId.Should().NotBe(first.UniqueId);
    }

    [Test]
    public async Task AddAsync_DuplicateUniqueId_ReturnsDuplicate()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "first" });

        // Act
        var second = await UuidRepository.AddAsync(
            new TestUuidEntity { UniqueId = uniqueId, Name = "second" },
            e => e.UniqueId == uniqueId);

        // Assert
        second.ShouldFailAs(OperationStatus.Duplicate);
    }

    [Test]
    public async Task GetAsync_ByUniqueId_ReturnsTheEntity()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first", Value = 42 }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        var fetched = (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).ShouldExist();

        // Assert
        fetched.Name.Should().Be("first");
        fetched.Value.Should().Be(42);
        fetched.UniqueId.Should().Be(added.UniqueId);
    }

    [Test]
    public async Task GetAsync_UnknownUniqueId_ReturnsNull()
    {
        // Act
        var unknownId = Guid.NewGuid();
        var fetched = await UuidRepository.GetAsync(e => e.UniqueId == unknownId);

        // Assert
        fetched.Should().BeNull();
    }

    /// <remarks>
    /// Callers build entities from data carrying only the public identifier, so the internal key is typically left
    /// at its default. The update has to locate the row by unique ID regardless.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_EntityWithoutInternalKey_StillUpdates()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        var detached = new TestUuidEntity { UniqueId = added.UniqueId, Name = "rebuilt from a payload" };
        var updated = await UuidRepository.UpdateAsync(detached, e => e.UniqueId == added.UniqueId);

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var stored = (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).ShouldExist();

        stored.Name.Should().Be("rebuilt from a payload");
    }

    [Test]
    public async Task DeleteAsync_ByUniqueId_RemovesTheEntity()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        var deleted = await UuidRepository.DeleteAsync(added.UniqueId);

        // Assert
        deleted.ShouldSucceedAs(OperationStatus.Deleted);
        (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_ByUniqueId_RemovesOnlyThatEntity()
    {
        // Arrange
        var kept = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "keep" }))
            .ShouldSucceedAs(OperationStatus.Added);
        var dropped = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "drop" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        var deleted = (await UuidRepository.DeleteAsync(dropped.UniqueId)).ShouldSucceedAs(OperationStatus.Deleted);

        // Assert
        deleted.UniqueId.Should().Be(dropped.UniqueId);

        var remaining = await UuidRepository.GetAllAsync();

        remaining.Should().ContainSingle().Which.UniqueId.Should().Be(kept.UniqueId);
    }

    [Test]
    public async Task DeleteAsync_UnknownUniqueId_ReturnsNotFound()
    {
        // Act
        var deleted = await UuidRepository.DeleteAsync(Guid.NewGuid());

        // Assert
        deleted.ShouldFailAs(OperationStatus.NotFound);
    }

    /// <remarks>
    /// The unique ID is identity, not data. Locating a row by other criteria and then writing an entity carrying a
    /// different unique ID over it would replace a different entity, and would rewrite that row's public identifier
    /// as a side effect of copying every scalar -- invalidating every outstanding reference to it.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_ByPredicateLocatingADifferentEntity_Throws()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(
            new TestUuidEntity { Name = "old name", Value = 1 })).ShouldSucceedAs(OperationStatus.Added);

        // Act -- a rebuilt entity carries a fresh unique ID, so it is a different entity by definition.
        var act = async () => await UuidRepository.UpdateAsync(
            new TestUuidEntity { Name = "new name", Value = 1 },
            e => e.Name == "old name");

        // Assert
        var assertion = await act.Should().ThrowAsync<InvalidOperationException>();

        assertion.WithMessage("*Entity mismatch*");

        var stored = (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).ShouldExist();

        stored.Name.Should().Be("old name", "the update was refused");
        stored.UniqueId.Should().Be(added.UniqueId, "the public identifier is never rewritten");
    }

    [Test]
    public async Task UpdateAsync_ByPredicateLocatingTheSameEntity_Succeeds()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(
            new TestUuidEntity { Name = "old name", Value = 1 })).ShouldSucceedAs(OperationStatus.Added);

        // Act -- located by business criteria, but carrying the located row's own unique ID.
        var updated = await UuidRepository.UpdateAsync(
            new TestUuidEntity { UniqueId = added.UniqueId, Name = "new name", Value = 1 },
            e => e.Name == "old name");

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var stored = (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).ShouldExist();

        stored.Name.Should().Be("new name");
        stored.UniqueId.Should().Be(added.UniqueId);
    }

    [Test]
    public async Task UpdateAsync_WithoutAPredicate_UpdatesTheEntityWithTheSameUniqueId()
    {
        // Arrange
        var added = (await UuidRepository.AddAsync(
            new TestUuidEntity { Name = "first", Value = 1 })).ShouldSucceedAs(OperationStatus.Added);

        // Act
        var updated = await UuidRepository.UpdateAsync(
            new TestUuidEntity { UniqueId = added.UniqueId, Name = "second", Value = 2 });

        // Assert
        updated.ShouldSucceedAs(OperationStatus.Updated);

        var stored = (await UuidRepository.GetAsync(e => e.UniqueId == added.UniqueId)).ShouldExist();

        stored.Name.Should().Be("second");
        stored.Value.Should().Be(2);
        stored.UniqueId.Should().Be(added.UniqueId);
    }

    [Test]
    public async Task UpdateAsync_WithoutAPredicate_UnknownUniqueId_ReturnsNotFound()
    {
        // Arrange
        await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" });

        // Act
        var updated = await UuidRepository.UpdateAsync(new TestUuidEntity { Name = "absent" });

        // Assert
        updated.ShouldFailAs(OperationStatus.NotFound);
    }

    [Test]
    public async Task UpdateAsync_WithoutAPredicate_LeavesOtherEntitiesAlone()
    {
        // Arrange
        var first = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "first" }))
            .ShouldSucceedAs(OperationStatus.Added);
        var second = (await UuidRepository.AddAsync(new TestUuidEntity { Name = "second" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act
        (await UuidRepository.UpdateAsync(new TestUuidEntity { UniqueId = first.UniqueId, Name = "first updated" }))
            .ShouldSucceedAs(OperationStatus.Updated);

        // Assert
        var untouched = (await UuidRepository.GetAsync(e => e.UniqueId == second.UniqueId)).ShouldExist();

        untouched.Name.Should().Be("second");
    }

    /// <remarks>
    /// The unique identifier carries its own unique index, so a predicate written against any other property can be
    /// blocked by a row it does not match. The insert fails and the re-read finds nothing, which is reported as a
    /// duplicate rather than as the "found" or "added" a caller would act on.
    /// </remarks>
    [Test]
    public async Task GetOrAddAsync_BlockedByConstraintThePredicateDoesNotDescribe_ReturnsDuplicate()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "alpha" });

        // Act
        var result = await UuidRepository.GetOrAddAsync(
            new TestUuidEntity { UniqueId = uniqueId, Name = "beta" },
            e => e.Name == "beta");

        // Assert
        result.ShouldFailAs(OperationStatus.Duplicate);
        (await UuidRepository.GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be("alpha");
    }

    [Test]
    public async Task AddAsync_BlockedByConstraintThePredicateDoesNotDescribe_ReturnsDuplicate()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "alpha" });

        // Act
        var result = await UuidRepository.AddAsync(
            new TestUuidEntity { UniqueId = uniqueId, Name = "beta" },
            e => e.Name == "beta");

        // Assert
        result.ShouldFailAs(OperationStatus.Duplicate);
        (await UuidRepository.GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be("alpha");
    }

    /// <remarks>
    /// With no predicate the constraint alone decides, and a collision is reported the same way as a matched one.
    /// </remarks>
    [Test]
    public async Task AddAsync_BlockedWithoutADuplicationPredicate_ReturnsDuplicate()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "alpha" });

        // Act
        var added = await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "beta" });

        // Assert
        added.ShouldFailAs(OperationStatus.Duplicate);
    }

    /// <remarks>
    /// Proves the unique index declared on the unique identifier entity base actually reaches the database, rather
    /// than being silently dropped by a mis-declared index attribute.
    /// </remarks>
    [Test]
    public async Task UniqueIdIndex_IsEnforcedByTheDatabase()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "first" });

        await using var dbContext = await ContextProvider.GetDbContextAsync();

        // Act
        dbContext.TestUuidEntities.Add(new TestUuidEntity { UniqueId = uniqueId, Name = "second" });

        var act = async () => await dbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <remarks>
    /// The insert is blocked by a row the predicate does not match, so the recovering update finds nothing either.
    /// That is reported as a duplicate, not as a conflict, which a caller would reasonably retry forever.
    /// </remarks>
    [Test]
    public async Task AddOrUpdateAsync_BlockedByConstraintThePredicateDoesNotDescribe_ReturnsDuplicate()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "alpha" });

        // Act
        var result = await UuidRepository.AddOrUpdateAsync(
            new TestUuidEntity { UniqueId = uniqueId, Name = "beta" },
            e => e.Name == "beta");

        // Assert
        result.ShouldFailAs(OperationStatus.Duplicate);
        (await UuidRepository.GetAllAsync()).Should().ContainSingle().Which.Name.Should().Be("alpha");
    }

    /// <remarks>
    /// The counterpart to the test above. A predicate written against the unique identifier describes the constraint
    /// which would reject a duplicate insert, so the blocking row is always reachable and the call updates it.
    /// Recovery from an insert which actually loses a race is covered by <see cref="RaceRecoveryTests"/>.
    /// </remarks>
    [Test]
    public async Task AddOrUpdateAsync_PredicateDescribesTheUniqueConstraint_UpdatesTheBlockingEntity()
    {
        // Arrange
        var uniqueId = Guid.NewGuid();

        await UuidRepository.AddAsync(new TestUuidEntity { UniqueId = uniqueId, Name = "alpha", Value = 1 });

        // Act
        var result = await UuidRepository.AddOrUpdateAsync(
            new TestUuidEntity { UniqueId = uniqueId, Name = "alpha", Value = 2 },
            e => e.UniqueId == uniqueId);

        // Assert
        result.ShouldSucceedAs(OperationStatus.Updated).Value.Should().Be(2);
    }

    /// <remarks>
    /// An update can collide with a unique index as well as an insert can. The unique ID base refuses to rewrite a
    /// unique ID, so this goes through a plain repository over the same entity, which does not.
    /// </remarks>
    [Test]
    public async Task UpdateAsync_ViolatingAUniqueIndex_ReturnsDuplicate()
    {
        // Arrange
        var repository = new PlainUuidEntityRepository(
            NullLogger<RepositoryBase<TestDbContext, long, TestUuidEntity>>.Instance,
            ContextProvider);

        var alpha = (await repository.AddAsync(new TestUuidEntity { Name = "alpha" }))
            .ShouldSucceedAs(OperationStatus.Added);
        var beta = (await repository.AddAsync(new TestUuidEntity { Name = "beta" }))
            .ShouldSucceedAs(OperationStatus.Added);

        // Act -- locates beta, but carries alpha's unique ID.
        var result = await repository.UpdateAsync(
            new TestUuidEntity { UniqueId = alpha.UniqueId, Name = "beta renamed" },
            e => e.Name == "beta");

        // Assert
        result.ShouldFailAs(OperationStatus.Duplicate);

        var stored = (await repository.GetAsync(e => e.UniqueId == beta.UniqueId)).ShouldExist();

        stored.Name.Should().Be("beta", "the rejected update left the row unchanged");
    }
}