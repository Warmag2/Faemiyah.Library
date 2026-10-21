using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Entities;
using Faemiyah.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Repository.Repositories;

/// <summary>
/// Base repository class for data handling.
/// </summary>
public abstract class TemporalRepositoryBase<TContext, TKey, TEntity>
    : RepositoryBase<TContext, TKey, TEntity>, ITemporalEntityRepository<TKey, TEntity>
    where TContext : DbContext
    where TKey : struct, IEquatable<TKey>, IComparable<TKey>
    where TEntity : TemporalEntityBase<TKey>, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalRepositoryBase{TContext, TKey, TEntity}"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="databaseContextProvider">The database context provider.</param>
    /// <param name="timeProvider">
    /// The source of the current time, or <b>null</b> to read the system clock. This is what
    /// <see cref="GetLatestValidAsync"/> asks for "now".
    /// </param>
    protected TemporalRepositoryBase(
        ILogger<TemporalRepositoryBase<TContext, TKey, TEntity>> logger,
        IDatabaseContextProvider<TContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetLatestValidAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        return await GetLatestValidAtAsync(
            TimeProvider.GetUtcNow().UtcDateTime,
            predicate,
            processIncludes,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetLatestValidAtAsync(
        DateTime pointInTime,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool processIncludes = false,
        CancellationToken cancellationToken = default)
    {
        if (pointInTime.Kind != DateTimeKind.Utc)
        {
            // Failing here beats a provider level cast error further down.
            throw new ArgumentException("The point in time must be UTC.", nameof(pointInTime));
        }

        await using var dbContext = await DatabaseContextProvider.GetDbContextAsync(cancellationToken: cancellationToken);

        var includeSet = GetIncludeSet(dbContext, processIncludes);
        var usedPredicate = RefineWith(
            e => e.DateBegin <= pointInTime && (e.DateEnd >= pointInTime || e.DateEnd == null),
            predicate);

        // Ordered rather than single, because several periods may cover the instant and the latest one wins.
        return await includeSet
            .AsNoTracking()
            .Where(usedPredicate)
            .OrderByDescending(e => e.DateBegin)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
