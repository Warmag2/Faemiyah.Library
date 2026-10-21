using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Models;

namespace Faemiyah.Repository.Interfaces;

/// <summary>
/// Interface for a generic entity repository.
/// </summary>
/// <remarks>
/// A <c>duplicationPredicate</c> is the rule for when two entities are the same one. It is only race-safe when a
/// unique index covers the columns it tests; <see cref="EntityBase{TKey}"/> declares none, so that is the caller's
/// job. An unindexed predicate which loses a race leaves two matching rows, after which every single-entity
/// operation throws for it until one is deleted.
/// </remarks>
/// <typeparam name="TKey">The key of the entity.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IEntityRepository<TKey, TEntity>
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : EntityBase<TKey>, new()
{
    /// <summary>
    /// Add an entity to the database.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="duplicationPredicate">A predicate function to check for collision. Leave at null to skip check. Advisory unless a unique index backs it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> with the entity and its database generated ID, or
    /// <see cref="OperationStatus.Duplicate"/> when <paramref name="duplicationPredicate"/> matched an entity or a
    /// unique constraint rejected the insert.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="duplicationPredicate"/> matches more than one entity.
    /// </exception>
    Task<RepositoryResult<TEntity>> AddAsync(TEntity entity, Expression<Func<TEntity, bool>>? duplicationPredicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add to, or update an entity in the database.
    /// </summary>
    /// <param name="entity">The entity to add or update.</param>
    /// <param name="duplicationPredicate">A predicate function to check for an existing entity to replace. Advisory unless a unique index backs it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> or <see cref="OperationStatus.Updated"/> with the written entity;
    /// <see cref="OperationStatus.Conflict"/> when the entity already existed and the stored entity has been modified
    /// since the revision this call is working from -- either the <see cref="EntityBase{TKey}.ConcurrencyToken"/> on
    /// <paramref name="entity"/>, or the stored one if it carries none -- or was removed part way through, so
    /// concurrent callers upserting the same entity should expect this and retry; or
    /// <see cref="OperationStatus.Duplicate"/> when a unique constraint the predicate does not describe rejected
    /// the write.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="duplicationPredicate"/> matches more than one entity.
    /// </exception>
    Task<RepositoryResult<TEntity>> AddOrUpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> duplicationPredicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete entities from the database based on a predicate.
    /// </summary>
    /// <param name="predicate">A predicate function to refine the deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Deleted"/> with the deleted entities, which are empty if none matched, or
    /// <see cref="OperationStatus.Conflict"/> when any of them is modified or removed by someone else between being
    /// read and being deleted, in which case none are deleted.
    /// </returns>
    Task<RepositoryResult<IReadOnlyCollection<TEntity>>> DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all entities from the database.
    /// </summary>
    /// <remarks>
    /// Every entity is materialised into memory at once. Use
    /// <see cref="GetPageAsync(Expression{Func{TEntity, bool}}, bool, TKey?, int?, CancellationToken)"/>
    /// for sets which may be large.
    /// </remarks>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// All entities in the database, or an empty collection, if none were found.
    /// </returns>
    Task<IReadOnlyCollection<TEntity>> GetAllAsync(
        bool processIncludes = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all entities from the database which match the given predicate.
    /// </summary>
    /// <remarks>
    /// Every matching entity is materialised into memory at once. Use
    /// <see cref="GetPageAsync(Expression{Func{TEntity, bool}}, bool, TKey?, int?, CancellationToken)"/>
    /// for sets which may be large. The predicate is required: call
    /// <see cref="GetAllAsync(bool, CancellationToken)"/> to fetch everything, so that a predicate which
    /// unexpectedly evaluated to <b>null</b> cannot silently widen the query to the whole table.
    /// </remarks>
    /// <param name="predicate">A predicate function to refine the search.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// All entities in the database which match the given predicate, or an empty collection, if none were found.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is <b>null</b>.</exception>
    Task<IReadOnlyCollection<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single page of entities, ordered by the internal database key.
    /// </summary>
    /// <param name="predicate">A predicate function to refine the search, or <b>null</b> for no refinement.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="continuationToken">The token returned by the previous page, or <b>null</b> to start from the beginning.</param>
    /// <param name="limit">
    /// The maximum number of entities in the page, or <b>null</b> to use the repository default.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of entities, and the token needed to fetch the next one.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than one.</exception>
    Task<PagedList<TKey, TEntity>> GetPageAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        TKey? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an entity from the database.
    /// </summary>
    /// <param name="predicate">A predicate function to refine the search. Must match at most one entity.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity, or <b>null</b>, if the entity could not be found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="predicate"/> matches more than one entity. Use
    /// <see cref="GetAllAsync(Expression{Func{TEntity, bool}}, bool, CancellationToken)"/> when a predicate may
    /// match several entities.
    /// </exception>
    Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an entity from the database, or add it if it does not exist.
    /// </summary>
    /// <param name="entity">The entity to add if it does not exist.</param>
    /// <param name="duplicationPredicate">A predicate function to check for collision. Advisory unless a unique index backs it.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> with the entity this call created, <see cref="OperationStatus.Found"/>
    /// with an existing one, including the winner of a lost insert race, or <see cref="OperationStatus.Duplicate"/>
    /// when a unique constraint rejected the insert but the predicate then matched none.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="duplicationPredicate"/> matches more than one entity.
    /// </exception>
    Task<RepositoryResult<TEntity>> GetOrAddAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> duplicationPredicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an entity in the database.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="duplicationPredicate">A predicate function to find the existing entity with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the updated entity; <see cref="OperationStatus.NotFound"/> when no
    /// entity matched <paramref name="duplicationPredicate"/>; <see cref="OperationStatus.Conflict"/> when the stored
    /// entity has been modified since the revision this call is working from, which is the
    /// <see cref="EntityBase{TKey}.ConcurrencyToken"/> on <paramref name="entity"/> when it carries one, and
    /// otherwise the stored revision as read at the start of this call; or <see cref="OperationStatus.Duplicate"/>
    /// when a unique constraint rejected the update.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="duplicationPredicate"/> matches more than one entity.
    /// </exception>
    Task<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> duplicationPredicate, CancellationToken cancellationToken = default);
}
