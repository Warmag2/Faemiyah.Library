using System;
using System.Linq.Expressions;
using AwesomeAssertions;
using Faemiyah.Repository.Helpers;
using NUnit.Framework;

namespace Faemiyah.Repository.UnitTests;

[TestFixture]
public class ExpressionHelperTests
{
    [Test]
    public void And_BothPredicatesSatisfied_Matches()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.And(first, second).Compile();

        // Assert
        combined(new Sample { Number = 10, Text = "yes" }).Should().BeTrue();
    }

    [Test]
    public void And_OnlyOnePredicateSatisfied_DoesNotMatch()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.And(first, second).Compile();

        // Assert
        combined(new Sample { Number = 10, Text = "no" }).Should().BeFalse();
        combined(new Sample { Number = 1, Text = "yes" }).Should().BeFalse();
    }

    [Test]
    public void Or_EitherPredicateSatisfied_Matches()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.Or(first, second).Compile();

        // Assert
        combined(new Sample { Number = 10, Text = "no" }).Should().BeTrue();
        combined(new Sample { Number = 1, Text = "yes" }).Should().BeTrue();
    }

    [Test]
    public void Or_NeitherPredicateSatisfied_DoesNotMatch()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.Or(first, second).Compile();

        // Assert
        combined(new Sample { Number = 1, Text = "no" }).Should().BeFalse();
    }

    /// <remarks>
    /// This is the property the whole helper exists for. Combining with <c>Expression.Invoke</c> would also
    /// evaluate correctly, but leaves nested lambdas in the tree which query providers may refuse to translate.
    /// </remarks>
    [Test]
    public void And_ProducesTreeWithoutInvocations()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.And(first, second);

        // Assert
        InvocationDetector.ContainsInvocation(combined).Should().BeFalse();
    }

    [Test]
    public void Or_ProducesTreeWithoutInvocations()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.Or(first, second);

        // Assert
        InvocationDetector.ContainsInvocation(combined).Should().BeFalse();
    }

    /// <remarks>
    /// Both source predicates declare their own parameter. The combined tree must reference exactly one, or a
    /// provider would see an unbound parameter on one side of the operator.
    /// </remarks>
    [Test]
    public void And_UnifiesParametersOfBothPredicates()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = other => other.Text == "yes";

        // Act
        var combined = ExpressionHelper.And(first, second);

        // Assert
        combined.Parameters.Should().HaveCount(1);
        ParameterCollector.DistinctParameters(combined.Body).Should().BeEquivalentTo(new[] { combined.Parameters[0] });
    }

    [Test]
    public void And_ThreePredicatesChained_Matches()
    {
        // Arrange
        Expression<Func<Sample, bool>> first = s => s.Number > 5;
        Expression<Func<Sample, bool>> second = s => s.Number < 20;
        Expression<Func<Sample, bool>> third = s => s.Text == "yes";

        // Act
        var combined = ExpressionHelper.And(ExpressionHelper.And(first, second), third);

        // Assert
        InvocationDetector.ContainsInvocation(combined).Should().BeFalse();
        combined.Compile()(new Sample { Number = 10, Text = "yes" }).Should().BeTrue();
        combined.Compile()(new Sample { Number = 30, Text = "yes" }).Should().BeFalse();
    }

    private sealed class Sample
    {
        public int Number { get; set; }

        public string? Text { get; set; }
    }
}
