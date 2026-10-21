using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Faemiyah.Repository.Helpers.ExpressionHelper;

namespace Faemiyah.Repository.Repositories;

/// <summary>
/// Base repository class for data handling.
/// </summary>
/// <remarks>
/// Every public operation acquires its own context and disposes it again. The protected
/// <c>...WithContextAsync</c> counterparts take a context instead, so a derived repository can compose several
/// operations onto one and save them together, which is what anything needing to be atomic across entities wants.
/// <para>
/// Since the context is created per operation rather than injected, an instance holds no caller state and
/// concurrent calls on one are safe, so register derived repositories as singletons. That holds only while a
/// derived repository adds no mutable state of its own.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The type of the database context.</typeparam>
/// <typeparam name="TKey">The type of the internal database key.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public abstract class RepositoryBase<TContext, TKey, TEntity> : IEntityRepository<TKey, TEntity>
    where TContext : DbContext
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : EntityBase<TKey>, new()
{
    /// <summary>
    /// The default maximum number of entities fetched in a single page.
    /// </summary>
    protected const int DefaultPageLimit = 1000;

    /// <summary>
    /// The database context provider.
    /// </summary>
    protected readonly IDatabaseContextProvider<TContext> DatabaseContextProvider;

    /// <summary>
    /// The logging interface.
    /// </summary>
    protected readonly ILogger<RepositoryBase<TContext, TKey, TEntity>> Logger;

    /// <summary>
    /// The source of the current time.
    /// </summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryBase{TContext, TKey, TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">
    /// The source of the current time, or <b>null</b> to read the system clock. Supply one to make the timestamps
    /// this repository writes controllable from a test.
    /// </param>
    protected RepositoryBase(
        ILogger<RepositoryBase<TContext, TKey, TEntity>> logger,
        IDatabaseContextProvider<TContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
    {
        Logger = logger;
        DatabaseContextProvider = databaseContextProvider;
        TimeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> AddAsync(TEntity entity, Expression<Func<TEntity, bool>>? duplicationPredicate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await AddWithContextAsync(dbContext, entity, duplicationPredicate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> AddOrUpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> duplicationPredicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        RequirePredicate(duplicationPredicate, nameof(AddOrUpdateAsync));

        await using var context = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);

        var foundEntity = await FindSingleEntityAsync(context.Set<TEntity>(), duplicationPredicate, cancellationToken);

        if (foundEntity != null)
        {
            return await TryUpdateWithContext(context, foundEntity, entity, cancellationToken);
        }

        var added = await TryInsertWithContextAsync(context, entity, cancellationToken);

        if (added.IsSuccess)
        {
            return added;
        }

        // A race winner now matches the predicate, so the update adopts it. Matching nothing means the blocker is a
        // constraint the predicate does not describe.
        var updated = await UpdateWithContextAsync(context, entity, duplicationPredicate, cancellationToken);

        return updated.Status == OperationStatus.NotFound ? added : updated;
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<IReadOnlyCollection<TEntity>>> DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await DeleteByPredicateWithContextAsync(dbContext, predicate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TEntity>> GetAllAsync(
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        return await FetchAllPagesAsync(null, processIncludes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        RequirePredicate(predicate, nameof(GetAllAsync));

        return await FetchAllPagesAsync(predicate, processIncludes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> GetOrAddAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> duplicationPredicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await GetOrAddWithContextAsync(
            dbContext,
            () => entity,
            duplicationPredicate,
            processIncludes,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedList<TKey, TEntity>> GetPageAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        TKey? continuationToken = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);

        var (items, hasMore) = await GetPageWithContextAsync(
            dbContext,
            predicate,
            processIncludes,
            continuationToken,
            limit ?? DefaultPageLimit,
            cancellationToken);

        return new PagedList<TKey, TEntity>
        {
            Items = items,

            ContinuationToken = hasMore ? items.Last().Id : null
        };
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await GetWithContextAsync(dbContext, predicate, processIncludes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> duplicationPredicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        RequirePredicate(duplicationPredicate, nameof(UpdateAsync));

        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);
        return await UpdateWithContextAsync(dbContext, entity, duplicationPredicate, cancellationToken);
    }

    /// <summary>
    /// Add an entity to the database using the given context.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="entity">The entity to add.</param>
    /// <param name="duplicationPredicate">A predicate to check for duplicate entities.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> with the added entity, or <see cref="OperationStatus.Duplicate"/> when
    /// <paramref name="duplicationPredicate"/> matched an entity or a unique constraint rejected the insert.
    /// </returns>
    protected async Task<RepositoryResult<TEntity>> AddWithContextAsync(
        TContext dbContext,
        TEntity entity,
        Expression<Func<TEntity, bool>>? duplicationPredicate = null,
        CancellationToken cancellationToken = default)
    {
        if (duplicationPredicate != null
            && await GetWithContextAsync(dbContext, duplicationPredicate, cancellationToken: cancellationToken) != null)
        {
            return RepositoryResult<TEntity>.Failure(OperationStatus.Duplicate);
        }

        return await TryInsertWithContextAsync(dbContext, entity, cancellationToken);
    }

    /// <summary>
    /// Delete every entity matching the given predicate, using the given context.
    /// </summary>
    /// <remarks>
    /// The entities are loaded because they are returned, so this is unsuitable for very large sets.
    /// </remarks>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="predicate">A predicate function to refine the deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Deleted"/> with the deleted entities, which are empty if none matched, or
    /// <see cref="OperationStatus.Conflict"/> when one was changed or removed by someone else meanwhile.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is <b>null</b>.</exception>
    protected async Task<RepositoryResult<IReadOnlyCollection<TEntity>>> DeleteByPredicateWithContextAsync(
        TContext dbContext,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        RequirePredicate(predicate, nameof(DeleteByPredicateWithContextAsync));

        var dbSet = dbContext.Set<TEntity>();

        var entities = await dbSet.Where(predicate).ToListAsync(cancellationToken);

        Logger.LogInformation(
            "Deleting {Count} entities of type {Type}",
            entities.Count,
            typeof(TEntity).Name);

        dbSet.RemoveRange(entities);

        return await SaveWithContextAsync<IReadOnlyCollection<TEntity>>(
            dbContext,
            OperationStatus.Deleted,
            entities,
            cancellationToken);
    }

    /// <summary>
    /// Find a single entity.
    /// </summary>
    /// <param name="includeSet">The set to search from.</param>
    /// <param name="predicate">The predicate for finding the entity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The found entity, or <b>null</b> if no entity was present.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the predicate matched more than one entity.</exception>
    protected async Task<TEntity?> FindSingleEntityAsync(IQueryable<TEntity> includeSet, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Fetching an entity of type {Type}.", typeof(TEntity).Name);

        // Two rows so an over-broad predicate can be reported. SingleOrDefaultAsync emits the same LIMIT 2.
        var results = await includeSet
            .Where(predicate)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (results.Count > 1)
        {
            Logger.LogError(
                "Predicate for operation {Operation} matched more than one entity of type {Type}.",
                nameof(GetWithContextAsync),
                typeof(TEntity).Name);

            throw new InvalidOperationException(
                $"The predicate matched more than one entity of type {typeof(TEntity).Name}. " +
                $"Use {nameof(GetAllAsync)} when a predicate may match several entities.");
        }

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Get the entity matching the given predicate, or add a generated one if no entity matches.
    /// </summary>
    /// <remarks>
    /// An entity from <paramref name="generator"/> must satisfy <paramref name="predicate"/>, or it is added and
    /// then not found next time, and added again. The <c>new TEntity()</c> fallback essentially never can, so
    /// supply a generator.
    /// </remarks>
    /// <param name="context">The database context to use.</param>
    /// <param name="generator">Produces the entity to add, or <b>null</b> to construct a default instance.</param>
    /// <param name="predicate">A predicate function identifying the entity. Must match at most one entity.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> with the entity this call created, <see cref="OperationStatus.Found"/>
    /// with an existing one, including the winner of a lost insert race, or <see cref="OperationStatus.Duplicate"/>
    /// when a unique constraint rejected the insert but <paramref name="predicate"/> matches no entity.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is <b>null</b>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="predicate"/> matches more than one entity.
    /// </exception>
    protected async Task<RepositoryResult<TEntity>> GetOrAddWithContextAsync(
        TContext context,
        Func<TEntity>? generator,
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        RequirePredicate(predicate, nameof(GetOrAddWithContextAsync));

        var result = await GetWithContextAsync(context, predicate, processIncludes, cancellationToken);

        if (result != null)
        {
            return new RepositoryResult<TEntity>(OperationStatus.Found, result);
        }

        var newEntity =
            generator == null
                ? new TEntity()
                : generator.Invoke();

        var added = await TryInsertWithContextAsync(context, newEntity, cancellationToken);

        if (added.IsSuccess)
        {
            return added;
        }

        // The winner of a lost race matches the predicate; anything else is a blocker we cannot reach.
        var winner = await GetWithContextAsync(context, predicate, processIncludes, cancellationToken);

        return winner == null ? added : new RepositoryResult<TEntity>(OperationStatus.Found, winner);
    }

    /// <summary>
    /// Get a single page of entities, ordered by the internal database key.
    /// </summary>
    /// <remarks>
    /// Keyset rather than offset based, so pages stay cheap however deep they are. Pass the <c>Id</c> of the last
    /// returned entity as the next continuation token, and stop once <c>HasMore</c> is <b>false</b>.
    /// </remarks>
    /// <param name="context">The database context to use.</param>
    /// <param name="predicate">A predicate function to refine the search, or <b>null</b> for no refinement.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="continuationToken">
    /// The key after which to start, or <b>null</b> to start from the beginning. These differ: a signed key can be
    /// negative, so "after the default key" would skip rows that "from the beginning" must return.
    /// </param>
    /// <param name="limit">The maximum number of entities to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The page of entities, and a boolean which is <b>true</b> when at least one more entity remains.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than one.</exception>
    protected async Task<(IReadOnlyCollection<TEntity> Items, bool HasMore)> GetPageWithContextAsync(
        TContext context,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        TKey? continuationToken = null,
        int limit = DefaultPageLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Page limit must be at least one.");
        }

        var includeSet = GetIncludeSet(context, processIncludes);

        Expression<Func<TEntity, bool>>? usedPredicate = predicate;

        if (continuationToken.HasValue)
        {
            var startAfter = continuationToken.Value;

            usedPredicate = RefineWith(e => e.Id.CompareTo(startAfter) > 0, predicate);
        }

        // Fetch one extra to determine if there are more items
        var fetchLimit = limit + 1;

        Logger.LogDebug("Fetching a page of entities of type {Type} with continuation token {ContinuationToken}.",
            typeof(TEntity).Name,
            continuationToken);

        var query = includeSet.AsNoTracking();

        if (usedPredicate != null)
        {
            query = query.Where(usedPredicate);
        }

        var result = await query
            .OrderBy(o => o.Id)
            .Take(fetchLimit)
            .ToListAsync(cancellationToken);

        var hasMore = result.Count > limit;
        var items = hasMore ? result.Take(limit).ToList() : result;

        return (items, hasMore);
    }

    /// <summary>
    /// Get the single entity matching the given predicate, using the given context.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="predicate">A predicate function to refine the search. Must match at most one entity.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entity, or <b>null</b>, if the entity could not be found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is <b>null</b>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="predicate"/> matches more than one entity.
    /// </exception>
    protected async Task<TEntity?> GetWithContextAsync(
        TContext dbContext,
        Expression<Func<TEntity, bool>> predicate,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        RequirePredicate(predicate, nameof(GetWithContextAsync));

        var includeSet = GetIncludeSet(dbContext, processIncludes);

        return await FindSingleEntityAsync(includeSet, predicate, cancellationToken);
    }

    /// <summary>
    /// Narrows a predicate the operation itself requires with an optional one from the caller.
    /// </summary>
    /// <param name="basePredicate">The predicate the operation always applies.</param>
    /// <param name="predicate">The caller's predicate, or <b>null</b> for no further refinement.</param>
    /// <returns>The combined predicate.</returns>
    protected static Expression<Func<TEntity, bool>> RefineWith(
        Expression<Func<TEntity, bool>> basePredicate,
        Expression<Func<TEntity, bool>>? predicate)
    {
        return predicate == null ? basePredicate : And(basePredicate, predicate);
    }

    /// <summary>
    /// Rejects a missing predicate on an operation which cannot be carried out without one.
    /// </summary>
    /// <param name="predicate">The predicate the caller supplied.</param>
    /// <param name="operation">The operation being attempted, for the log entry.</param>
    /// <param name="parameterName">The caller's name for the predicate, supplied by the compiler.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is <b>null</b>.</exception>
    protected void RequirePredicate(
        Expression<Func<TEntity, bool>>? predicate,
        string operation,
        [CallerArgumentExpression(nameof(predicate))] string? parameterName = null)
    {
        if (predicate is not null)
        {
            return;
        }

        Logger.LogError(
            "Predicate is not defined for operation {Operation} for entities of type {Type}.",
            operation,
            typeof(TEntity).Name);

        throw new ArgumentNullException(parameterName);
    }

    /// <summary>
    /// Saves the pending changes of the given context, reporting the expected failures of concurrent use.
    /// </summary>
    /// <remarks>
    /// A failed save leaves nothing saved, so the change tracker is cleared, or the next save on this context would
    /// retry it. Use this for every save a derived repository composes, so its callers see the same outcomes.
    /// </remarks>
    /// <typeparam name="T">The type of the value to report.</typeparam>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="status">The status to report on success.</param>
    /// <param name="value">The value to report on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <paramref name="status"/> with <paramref name="value"/>, <see cref="OperationStatus.Conflict"/> when a stored
    /// entity was changed or removed meanwhile, or <see cref="OperationStatus.Duplicate"/> when a unique constraint
    /// rejected the save.
    /// </returns>
    protected async Task<RepositoryResult<T>> SaveWithContextAsync<T>(
        TContext dbContext,
        OperationStatus status,
        T value,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RepositoryResult<T>(status, value);
        }
        catch (DbUpdateConcurrencyException e)
        {
            dbContext.ChangeTracker.Clear();

            Logger.LogInformation(e, "Concurrency conflict when saving entities of type {Type}.", typeof(TEntity).Name);

            return RepositoryResult<T>.Failure(OperationStatus.Conflict);
        }
        catch (DbUpdateException e) when (IsUniqueConstraintViolation(e))
        {
            dbContext.ChangeTracker.Clear();

            Logger.LogInformation(e, "Unique constraint collision when saving entities of type {Type}.", typeof(TEntity).Name);

            return RepositoryResult<T>.Failure(OperationStatus.Duplicate);
        }
    }

    /// <summary>
    /// Inserts an entity, reporting whether the insert survived a competing one.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Added"/> with the entity, or <see cref="OperationStatus.Duplicate"/> when a unique
    /// constraint rejected it.
    /// </returns>
    protected async Task<RepositoryResult<TEntity>> TryInsertWithContextAsync(
        TContext dbContext,
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        entity.UpdateTime = TimeProvider.GetUtcNow().UtcDateTime;

        dbContext.Set<TEntity>().Add(entity);

        Logger.LogInformation("Adding an entity of type {Type}.", typeof(TEntity).Name);

        return await SaveWithContextAsync(dbContext, OperationStatus.Added, entity, cancellationToken);
    }

    /// <summary>
    /// Updates an entity, using the given context and a specific existing entity.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="existingEntity">The existing tracked entity.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the tracked entity, <see cref="OperationStatus.Conflict"/> when the
    /// stored revision differs, or <see cref="OperationStatus.Duplicate"/> when a unique constraint rejected it.
    /// </returns>
    protected async Task<RepositoryResult<TEntity>> TryUpdateWithContext(TContext dbContext, TEntity existingEntity, TEntity entity, CancellationToken cancellationToken)
    {
        RequireMatchingIdentity(entity, existingEntity);

        entity.Id = existingEntity.Id;
        entity.UpdateTime = TimeProvider.GetUtcNow().UtcDateTime;

        if (entity.ConcurrencyToken == default)
        {
            // Constructed rather than read, so adopt the stored revision. Must precede SetValues, which overwrites
            // the tracked instance.
            entity.ConcurrencyToken = existingEntity.ConcurrencyToken;
        }

        var entry = dbContext.Entry(existingEntity);

        entry.CurrentValues.SetValues(entity);

        // EF builds the UPDATE's WHERE clause from OriginalValues, which was snapshotted when foundEntity was loaded
        // above -- so it holds our own read, and checking against it would compare a value to itself. Overwriting it
        // with the revision on the entity is what makes the check real. Attaching the caller's entity would let EF
        // snapshot this naturally, but throws when the entity is already tracked on this context, which derived
        // repositories may legitimately do.
        entry.OriginalValues[nameof(EntityBase<>.ConcurrencyToken)] = entity.ConcurrencyToken;

        Logger.LogInformation(
            "Updating an entity of type {Type}.",
            typeof(TEntity).Name);

        return await SaveWithContextAsync(dbContext, OperationStatus.Updated, existingEntity, cancellationToken);
    }

    /// <summary>
    /// Update an entity in the database using the given context.
    /// </summary>
    /// <remarks>
    /// Located by the duplication predicate rather than internal key, since callers do not know database IDs.
    /// All scalar values are copied, so this replaces rather than patches.
    /// </remarks>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="duplicationPredicate">A predicate to find the entity to update with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="OperationStatus.Updated"/> with the updated entity, <see cref="OperationStatus.NotFound"/> when no
    /// entity matched <paramref name="duplicationPredicate"/>, <see cref="OperationStatus.Conflict"/> when
    /// <paramref name="entity"/> carries a non-default <see cref="EntityBase{TKey}.ConcurrencyToken"/> and the stored
    /// entity has been modified since that token was read, or <see cref="OperationStatus.Duplicate"/> when a unique
    /// constraint rejected the update.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="duplicationPredicate"/> is <b>null</b>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="duplicationPredicate"/> matches more than one entity.
    /// </exception>
    protected async Task<RepositoryResult<TEntity>> UpdateWithContextAsync(
        TContext dbContext,
        TEntity entity,
        Expression<Func<TEntity, bool>> duplicationPredicate,
        CancellationToken cancellationToken = default)
    {
        RequirePredicate(duplicationPredicate, nameof(UpdateWithContextAsync));

        var dbSet = dbContext.Set<TEntity>();

        // Tracked, because the located entity is what gets mutated and saved. Two rows so an over-broad predicate is
        // reported the same way the read paths report it, rather than as a bare "sequence contains more than one".
        var matches = await dbSet
            .Where(duplicationPredicate)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
        {
            Logger.LogError(
                "Predicate for operation {Operation} matched more than one entity of type {Type}.",
                nameof(UpdateWithContextAsync),
                typeof(TEntity).Name);

            throw new InvalidOperationException(
                $"The predicate matched more than one entity of type {typeof(TEntity).Name}. " +
                $"An update must identify a single entity.");
        }

        var foundEntity = matches.FirstOrDefault();

        if (foundEntity == null)
        {
            Logger.LogError(
                "Unable to update. Entity of type {Type} with the given predicate does not exist.",
                typeof(TEntity).Name);

            return RepositoryResult<TEntity>.Failure(OperationStatus.NotFound);
        }

        return await TryUpdateWithContext(dbContext, foundEntity, entity, cancellationToken);
    }

    /// <summary>
    /// Verifies that the entity the caller supplied is the same entity as the one the predicate located.
    /// </summary>
    /// <remarks>
    /// The base repository has no notion of identity beyond the predicate itself -- the predicate <i>is</i> the rule
    /// about which entity is which -- so nothing is checked here. A derived repository whose entity carries its own
    /// identifier overrides this to reject an update which would write one entity's values over another's.
    /// </remarks>
    /// <param name="source">The entity supplied by the caller.</param>
    /// <param name="stored">The entity located by the duplication predicate.</param>
    protected virtual void RequireMatchingIdentity(TEntity source, TEntity stored)
    {
    }

    /// <summary>
    /// Get the queryable for this entity type, with related data included when requested.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <returns>The queryable to build the rest of the query on.</returns>
    protected IQueryable<TEntity> GetIncludeSet(DbContext dbContext, bool processIncludes)
    {
        var dbSet = dbContext.Set<TEntity>();

        return processIncludes ? ProcessIncludes(dbSet) : dbSet;
    }

    /// <summary>
    /// Apply the related data a derived repository wants loaded alongside this entity.
    /// </summary>
    /// <remarks>
    /// Includes nothing by default. The result is used whenever a caller passes <c>processIncludes: true</c>.
    /// </remarks>
    /// <param name="dbSet">The set for this entity type.</param>
    /// <returns>The queryable with includes applied.</returns>
    /// <example>
    /// <code>
    /// protected override IQueryable&lt;Order&gt; ProcessIncludes(DbSet&lt;Order&gt; dbSet)
    /// {
    ///     return dbSet.Include(o =&gt; o.Lines).ThenInclude(l =&gt; l.Product);
    /// }
    /// </code>
    /// </example>
    protected virtual IQueryable<TEntity> ProcessIncludes(DbSet<TEntity> dbSet)
    {
        return dbSet;
    }

    /// <summary>
    /// Whether the given exception represents a unique constraint violation.
    /// </summary>
    /// <remarks>
    /// EF Core does not normalise constraint violations across providers, so this has to inspect a provider
    /// specific error code. Override it when using a provider other than PostgreSQL.
    /// </remarks>
    /// <param name="exception">The exception raised while saving.</param>
    /// <returns>True if the exception was caused by a unique constraint violation; otherwise, false.</returns>
    protected virtual bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception?.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    /// <summary>
    /// Fetches every entity matching the given predicate, page by page.
    /// </summary>
    /// <remarks>
    /// Takes the predicate unchecked, so the fetch-everything overload can pass null while the other still rejects it.
    /// </remarks>
    /// <param name="predicate">A predicate function to refine the search, or <b>null</b> for no refinement.</param>
    /// <param name="processIncludes">Whether to process includes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching entities.</returns>
    private async Task<IReadOnlyCollection<TEntity>> FetchAllPagesAsync(
        Expression<Func<TEntity, bool>>? predicate,
        bool processIncludes,
        CancellationToken cancellationToken)
    {
        TKey? continuationToken = null;
        List<TEntity> entities = [];
        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);

        while (true)
        {
            (var items, var hasMore) = await GetPageWithContextAsync(
                dbContext,
                predicate,
                processIncludes,
                continuationToken,
                cancellationToken: cancellationToken);

            entities.AddRange(items);

            if (!hasMore)
            {
                break;
            }

            continuationToken = items.Last().Id;
        }

        return entities;
    }
}
