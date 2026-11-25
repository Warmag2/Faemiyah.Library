using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Models;

namespace Faemiyah.Repository.Interfaces;

/// <summary>
/// Interface for a generic entity repository with unique identifiers.
/// </summary>
/// <typeparam name="TKey">The key of the entity.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IEntityRepositoryWithUuid<TKey, TEntity> : IEntityRepository<TKey, TEntity>
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : EntityBaseWithUuid<TKey>, new()
{
    /// <summary>
    /// Delete an entity from the database.
    /// </summary>
    /// <param name="uniqueId">The unique ID of the entity which is to be deleted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Deleted"/> with the deleted entity, <see cref="OperationStatus.NotFound"/> when no
    /// entity has the ID, or <see cref="OperationStatus.Conflict"/> when it is modified or removed by someone else
    /// between being read and being deleted.
    /// </returns>
    Task<RepositoryResult<TEntity>> DeleteAsync(Guid uniqueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an entity from the database by its unique ID.
    /// </summary>
    /// <param name="uniqueId">The unique ID of the entity.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity, or <b>null</b>, if no entity has the ID.</returns>
    Task<TEntity?> GetAsync(Guid uniqueId, bool processIncludes = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an entity in the database, identified by its unique ID.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the updated entity, <see cref="OperationStatus.NotFound"/> when no
    /// entity has the same unique ID, <see cref="OperationStatus.Conflict"/> when the stored entity has been modified
    /// since the revision this call is working from, or <see cref="OperationStatus.Duplicate"/> when a unique
    /// constraint rejected the update.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is <b>null</b>.</exception>
    Task<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update several entities in the database, each identified by its unique ID, saving them together.
    /// </summary>
    /// <remarks>
    /// Either every entity is updated or none is, and on failure the entities are left as supplied.
    /// </remarks>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the updated entities, in the order supplied;
    /// <see cref="OperationStatus.NotFound"/> when no entity has one of the unique IDs;
    /// <see cref="OperationStatus.Conflict"/> when a stored entity has been modified since the revision this call is
    /// working from; or <see cref="OperationStatus.Duplicate"/> when a unique constraint rejected an update.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is <b>null</b>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="entities"/> contains <b>null</b>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when two entities carry the same unique ID.</exception>
    Task<RepositoryResult<IReadOnlyCollection<TEntity>>> UpdateManyAsync(
        IReadOnlyCollection<TEntity> entities,
        CancellationToken cancellationToken = default);
}
