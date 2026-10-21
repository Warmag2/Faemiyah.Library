using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Repository.Interfaces;

/// <summary>
/// Interface for a generic entity repository with temporal validity information.
/// </summary>
/// <typeparam name="TKey">The key of the entity.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface ITemporalEntityRepository<TKey, TEntity> : IEntityRepository<TKey, TEntity>
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : TemporalEntityBase<TKey>, new()
{
    /// <summary>
    /// Get the most recently started entity whose validity period covers the current instant.
    /// </summary>
    /// <remarks>
    /// Both bounds are inclusive and compared at full precision, so a period starting partway through a day is not
    /// valid earlier that day. An entity with no <see cref="TemporalEntityBase{TKey}.DateBegin"/> is never valid.
    /// Where several are valid at once, the one which started latest wins.
    /// </remarks>
    /// <param name="predicate">A predicate function to refine the search, or <b>null</b> for no refinement.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity, or <b>null</b> if no entity is currently valid.</returns>
    Task<TEntity?> GetLatestValidAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the most recently started entity whose validity period covers the given instant.
    /// </summary>
    /// <remarks>
    /// The same rule as <see cref="GetLatestValidAsync"/>, against a supplied instant rather than the current one.
    /// </remarks>
    /// <param name="pointInTime">The instant to evaluate validity at. Must be UTC.</param>
    /// <param name="predicate">A predicate function to refine the search, or <b>null</b> for no refinement.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity, or <b>null</b> if no entity is valid at that instant.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pointInTime"/> is not <see cref="DateTimeKind.Utc"/>.
    /// </exception>
    Task<TEntity?> GetLatestValidAtAsync(
        DateTime pointInTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        CancellationToken cancellationToken = default);
}
