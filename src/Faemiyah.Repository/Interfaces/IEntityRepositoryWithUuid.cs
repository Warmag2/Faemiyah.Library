using System;
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
}
