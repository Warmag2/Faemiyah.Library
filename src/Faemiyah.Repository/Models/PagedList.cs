using System.Collections.Generic;

namespace Faemiyah.Repository.Models;

/// <summary>
/// One page of entities, together with the information needed to fetch the next one.
/// </summary>
/// <remarks>
/// No total count is exposed on purpose: keyset paging is cheap because it never counts, and an exact count over the
/// filtered set costs a full scan on every page.
/// </remarks>
/// <typeparam name="TKey">The type of the internal database key, which is also the continuation token type.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public sealed record PagedList<TKey, TEntity>
    where TKey : struct
{
    /// <summary>
    /// The entities in this page.
    /// </summary>
    public IReadOnlyCollection<TEntity> Items { get; init; } = [];

    /// <summary>
    /// The token to pass to the next call, or <b>null</b> when this is the last page.
    /// </summary>
    public TKey? ContinuationToken { get; init; }

    /// <summary>
    /// Whether at least one more entity remains beyond this page.
    /// </summary>
    public bool HasMore => ContinuationToken.HasValue;
}
