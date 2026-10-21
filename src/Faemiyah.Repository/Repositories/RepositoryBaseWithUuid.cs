using System;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Repository.Repositories;

/// <summary>
/// Base repository class for data handling with unique identifier -specific methods.
/// </summary>
/// <typeparam name="TContext">The type of the database context.</typeparam>
/// <typeparam name="TKey">The type of the internal database key.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public abstract class RepositoryBaseWithUuid<TContext, TKey, TEntity> : RepositoryBase<TContext, TKey, TEntity>, IEntityRepositoryWithUuid<TKey, TEntity>
    where TContext : DbContext
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : EntityBaseWithUuid<TKey>, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryBase{TContext, TKey, TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">
    /// The source of the current time, or <b>null</b> to read the system clock. Supply one to make the timestamps
    /// this repository writes controllable from a test.
    /// </param>
    protected RepositoryBaseWithUuid(
        ILogger<RepositoryBaseWithUuid<TContext, TKey, TEntity>> logger,
        IDatabaseContextProvider<TContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Hoisted so the expression closes over a plain Guid, which EF parameterises, rather than over the entity.
        var uniqueId = entity.UniqueId;

        return await UpdateAsync(entity, e => e.UniqueId == uniqueId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> DeleteAsync(Guid uniqueId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await DeleteWithContextAsync(dbContext, uniqueId, cancellationToken);
    }

    /// <summary>
    /// Delete an entity from the database using the given context.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="uniqueId">The unique ID of the entity which is to be deleted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Deleted"/> with the deleted entity, <see cref="OperationStatus.NotFound"/> when no
    /// entity has the ID, or <see cref="OperationStatus.Conflict"/> when it was changed or removed meanwhile.
    /// </returns>
    protected async Task<RepositoryResult<TEntity>> DeleteWithContextAsync(
        TContext dbContext,
        Guid uniqueId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = dbContext.Set<TEntity>();

        var entity = await FindSingleEntityAsync(
            dbSet,
            e => e.UniqueId == uniqueId,
            cancellationToken: cancellationToken);

        if (entity == null)
        {
            Logger.LogWarning(
                "Unable to delete. Entity of type {Type} with unique ID {UniqueId} does not exist.",
                typeof(TEntity).Name,
                uniqueId);

            return RepositoryResult<TEntity>.Failure(OperationStatus.NotFound);
        }

        dbSet.Remove(entity);

        Logger.LogInformation("Deleting an entity of type {Type} with unique ID {UniqueId}.",
            typeof(TEntity).Name,
            uniqueId);

        return await SaveWithContextAsync(dbContext, OperationStatus.Deleted, entity, cancellationToken);
    }

    /// <summary>
    /// Rejects an update between entities whose identity does not match.
    /// </summary>
    /// <param name="source">The entity supplied by the caller.</param>
    /// <param name="stored">The entity located by the duplication predicate.</param>
    /// <exception cref="InvalidOperationException">Thrown when the two carry different unique IDs.</exception>
    protected override void RequireMatchingIdentity(TEntity source, TEntity stored)
    {
        if (source.UniqueId == stored.UniqueId)
        {
            return;
        }

        Logger.LogError(
            "Unable to update. Entity of type {Type} with unique ID {SourceUniqueId} does not match the located "
            + "entity with unique ID {StoredUniqueId}.",
            typeof(TEntity).Name,
            source.UniqueId,
            stored.UniqueId);

        throw new InvalidOperationException(
            $"Entity mismatch. The predicate located an entity of type {typeof(TEntity).Name} with unique ID " +
            $"{stored.UniqueId}, while the supplied entity possesses unique ID {source.UniqueId}.");
    }
}
