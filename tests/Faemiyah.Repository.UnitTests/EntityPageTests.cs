using AwesomeAssertions;
using Faemiyah.Repository.Models;
using NUnit.Framework;

namespace Faemiyah.Repository.UnitTests;

[TestFixture]
public class EntityPageTests
{
    [Test]
    public void HasMore_WithoutContinuationToken_IsFalse()
    {
        // Arrange
        var page = new PagedList<long, string> { Items = ["a"] };

        // Assert
        page.HasMore.Should().BeFalse();
    }

    [Test]
    public void HasMore_WithContinuationToken_IsTrue()
    {
        // Arrange
        var page = new PagedList<long, string> { Items = ["a"], ContinuationToken = 7 };

        // Assert
        page.HasMore.Should().BeTrue();
        page.ContinuationToken.Should().Be(7);
    }

    [Test]
    public void Items_WhenNotSet_IsEmptyRatherThanNull()
    {
        // Arrange
        var page = new PagedList<long, string>();

        // Assert
        page.Items.Should().NotBeNull();
        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
    }
}
