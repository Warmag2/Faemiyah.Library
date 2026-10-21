using System.Diagnostics.CodeAnalysis;

namespace Faemiyah.Repository.Models;

/// <summary>
/// The outcome of a repository write, with the written value on success.
/// </summary>
/// <remarks>
/// Expected outcomes of concurrent use are reported here rather than thrown. Exceptions are left for programming
/// errors, such as an ambiguous predicate, and for infrastructure failures.
/// </remarks>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Status">The outcome.</param>
/// <param name="Value">The written value on success, otherwise <b>null</b>.</param>
public sealed record RepositoryResult<T>(OperationStatus Status, T? Value)
    where T : class
{
    /// <summary>
    /// Whether the operation succeeded, and so carries a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Status is OperationStatus.Found or OperationStatus.Added or OperationStatus.Updated or OperationStatus.Deleted;

    /// <summary>
    /// A failed result.
    /// </summary>
    /// <param name="status">The reason.</param>
    /// <returns>The result.</returns>
    public static RepositoryResult<T> Failure(OperationStatus status)
    {
        return new RepositoryResult<T>(status, null);
    }
}
