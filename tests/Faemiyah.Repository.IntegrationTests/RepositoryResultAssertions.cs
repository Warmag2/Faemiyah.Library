using AwesomeAssertions;
using Faemiyah.Repository.Models;

namespace Faemiyah.Repository.IntegrationTests;

/// <summary>
/// Helpers for consuming the results the repository returns.
/// </summary>
internal static class RepositoryResultAssertions
{
    /// <summary>
    /// Asserts that a result is present and returns it, so the caller can go on using it without a null check.
    /// </summary>
    /// <remarks>
    /// Reads return null to report "not found", so a test which needs the entity has to establish that it is there.
    /// Doing it this way reports a failed assertion rather than a null reference exception further down the test.
    /// </remarks>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="value">The result to check.</param>
    /// <param name="because">Why the result is expected to be present.</param>
    /// <returns>The result, known to be present.</returns>
    public static T ShouldExist<T>(this T? value, string because = "")
        where T : class
    {
        value.Should().NotBeNull(because);

        return value;
    }

    /// <summary>
    /// Asserts that a write succeeded with the given status and returns its value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="status">The success status expected.</param>
    /// <param name="because">Why the status is expected.</param>
    /// <returns>The value, known to be present.</returns>
    public static T ShouldSucceedAs<T>(this RepositoryResult<T> result, OperationStatus status, string because = "")
        where T : class
    {
        result.Should().NotBeNull(because);
        result.Status.Should().Be(status, because);
        result.IsSuccess.Should().BeTrue(because);
        result.Value.Should().NotBeNull(because);

        return result.Value;
    }

    /// <summary>
    /// Asserts that a write failed with the given status and carries no value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="status">The failure status expected.</param>
    /// <param name="because">Why the status is expected.</param>
    public static void ShouldFailAs<T>(this RepositoryResult<T> result, OperationStatus status, string because = "")
        where T : class
    {
        result.Should().NotBeNull(because);
        result.Status.Should().Be(status, because);
        result.IsSuccess.Should().BeFalse(because);
        result.Value.Should().BeNull(because);
    }
}