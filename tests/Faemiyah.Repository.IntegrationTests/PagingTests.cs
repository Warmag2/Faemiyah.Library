using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Repository.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Faemiyah.Repository.IntegrationTests;

[TestFixture]
public class PagingTests : IntegrationTestBase
{
    /// <summary>
    /// Enough entities to require more than one page at the repository's own default page size of 1000, so that
    /// the loop inside <c>GetAllAsync</c> is actually iterated rather than satisfied by a single page.
    /// </summary>
    private const int MoreThanOneDefaultPage = 1001;

    [Test]
    public async Task GetPageAsync_FewerEntitiesThanLimit_ReportsNoMore()
    {
        // Arrange
        await SeedAsync(3);

        // Act
        var page = await Repository.GetPageAsync(limit: 10);

        // Assert
        page.Items.Should().HaveCount(3);
        page.HasMore.Should().BeFalse();
        page.ContinuationToken.Should().BeNull();
    }

    /// <remarks>
    /// The boundary case. One extra entity is fetched to detect a successor, so an exact fit is where an
    /// off-by-one would show up as a spurious empty final page.
    /// </remarks>
    [Test]
    public async Task GetPageAsync_ExactlyAsManyEntitiesAsLimit_ReportsNoMore()
    {
        // Arrange
        await SeedAsync(5);

        // Act
        var page = await Repository.GetPageAsync(limit: 5);

        // Assert
        page.Items.Should().HaveCount(5);
        page.HasMore.Should().BeFalse();
        page.ContinuationToken.Should().BeNull();
    }

    [Test]
    public async Task GetPageAsync_MoreEntitiesThanLimit_ReportsMoreAndYieldsToken()
    {
        // Arrange
        await SeedAsync(6);

        // Act
        var page = await Repository.GetPageAsync(limit: 5);

        // Assert
        page.Items.Should().HaveCount(5);
        page.HasMore.Should().BeTrue();
        page.ContinuationToken.Should().Be(page.Items.Last().Id);
    }

    /// <remarks>
    /// Walking the token must visit every entity exactly once. Repeating the boundary row, or skipping it, is the
    /// classic keyset paging defect.
    /// </remarks>
    [Test]
    public async Task GetPageAsync_WalkingTheToken_VisitsEveryEntityExactlyOnce()
    {
        // Arrange
        var seeded = await SeedAsync(23);
        var seen = new List<string?>();
        long? token = null;

        // Act
        do
        {
            var page = await Repository.GetPageAsync(continuationToken: token, limit: 5);

            seen.AddRange(page.Items.Select(e => e.Name));
            token = page.ContinuationToken;
        }
        while (token != null);

        // Assert
        seen.Should().HaveCount(23);
        seen.Should().OnlyHaveUniqueItems();
        seen.Should().BeEquivalentTo(seeded.Select(e => e.Name));
    }

    [Test]
    public async Task GetPageAsync_WithPredicate_PagesOnlyMatchingEntities()
    {
        // Arrange
        await SeedAsync(10);

        // Act
        var page = await Repository.GetPageAsync(e => e.Value % 2 == 0, limit: 100);

        // Assert
        page.Items.Should().HaveCount(5);
        page.Items.Should().AllSatisfy(e => (e.Value % 2).Should().Be(0));
        page.HasMore.Should().BeFalse();
    }

    [Test]
    public async Task GetPageAsync_LimitBelowOne_Throws()
    {
        // Act
        var act = async () => await Repository.GetPageAsync(limit: 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetPageAsync_EmptyTable_ReturnsEmptyPage()
    {
        // Act
        var page = await Repository.GetPageAsync();

        // Assert
        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
    }

    /// <remarks>
    /// Exercises the paging loop inside <c>GetAllAsync</c>, which only iterates when the set exceeds the default
    /// page size, and which advances by reading the key of the last entity of each page.
    /// </remarks>
    [Test]
    public async Task GetAllAsync_MoreEntitiesThanOnePage_ReturnsEveryEntityOnce()
    {
        // Arrange
        await SeedAsync(MoreThanOneDefaultPage);

        // Act
        var all = await Repository.GetAllAsync();

        // Assert
        all.Should().HaveCount(MoreThanOneDefaultPage);
        all.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Inserts entities in one round trip, rather than one call each, so that the larger cases stay quick.
    /// </summary>
    /// <param name="count">How many entities to insert.</param>
    /// <returns>The inserted entities.</returns>
    /// <remarks>
    /// The keyset filter used to be applied unconditionally against a continuation token that defaulted to
    /// <c>default(TKey)</c>, which made "from the beginning" mean "after key zero". A signed key is free to be
    /// negative, and PostgreSQL accepts an explicit one because the column is generated by default rather than
    /// always, so such rows were stored and then silently omitted from every paged read.
    /// </remarks>
    [Test]
    public async Task GetAllAsync_EntityWithNegativeKey_IsReturned()
    {
        // Arrange
        await SeedWithNegativeKeyAsync();

        // Act
        var all = await Repository.GetAllAsync();

        // Assert
        all.Should().Contain(e => e.Id == -1);
        all.Should().HaveCount(3);
    }

    [Test]
    public async Task GetPageAsync_EntityWithNegativeKey_IsReturnedFirst()
    {
        // Arrange
        await SeedWithNegativeKeyAsync();

        // Act
        var page = await Repository.GetPageAsync(limit: 10);

        // Assert
        page.Items.First().Id.Should().Be(-1);
        page.Items.Should().HaveCount(3);
    }

    /// <remarks>
    /// The other half of the distinction: a token which is genuinely present must still exclude its own key, so
    /// restoring the negative rows must not turn paging into a loop which repeats the last entity.
    /// </remarks>
    [Test]
    public async Task GetPageAsync_ContinuationTokenOfNegativeKey_SkipsThatEntity()
    {
        // Arrange
        await SeedWithNegativeKeyAsync();

        // Act
        var page = await Repository.GetPageAsync(continuationToken: -1, limit: 10);

        // Assert
        page.Items.Should().NotContain(e => e.Id == -1);
        page.Items.Should().HaveCount(2);
    }

    private async Task SeedWithNegativeKeyAsync()
    {
        await using var dbContext = await ContextProvider.GetDbContextAsync();

        dbContext.TestEntities.Add(new TestEntity { Id = -1, Name = "negative" });
        dbContext.TestEntities.Add(new TestEntity { Name = "generated-one" });
        dbContext.TestEntities.Add(new TestEntity { Name = "generated-two" });

        await dbContext.SaveChangesAsync();
    }

    private async Task<IReadOnlyCollection<TestEntity>> SeedAsync(int count)
    {
        await using var dbContext = await ContextProvider.GetDbContextAsync();

        var entities = Enumerable.Range(0, count)
            .Select(i => new TestEntity { Name = $"entity-{i:D5}", Value = i })
            .ToList();

        dbContext.TestEntities.AddRange(entities);

        await dbContext.SaveChangesAsync();

        return entities;
    }
}
