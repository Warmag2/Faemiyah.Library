using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A concrete repository for <see cref="TestEntity"/>, overriding include processing so that it can be exercised.
/// </summary>
public class TestEntityRepository : RepositoryBase<TestDbContext, long, TestEntity>
{
    public TestEntityRepository(
        ILogger<RepositoryBase<TestDbContext, long, TestEntity>> logger,
        IDatabaseContextProvider<TestDbContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }

    protected override IQueryable<TestEntity> ProcessIncludes(DbSet<TestEntity> dbSet)
    {
        return dbSet.Include(e => e.Children);
    }

    /// <summary>
    /// Exposes <see cref="RepositoryBase{TContext, TKey, TEntity}.UpdateWithContextAsync"/>, so a test can compose
    /// operations onto one context.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="predicate">The predicate locating the entity.</param>
    /// <returns>The result of the update.</returns>
    public async Task<RepositoryResult<TestEntity>> UpdateUsingContextAsync(
        TestDbContext dbContext,
        TestEntity entity,
        Expression<Func<TestEntity, bool>> predicate)
    {
        return await UpdateWithContextAsync(dbContext, entity, predicate);
    }

    /// <summary>
    /// Exposes <see cref="RepositoryBase{TContext, TKey, TEntity}.AddWithContextAsync"/>, so a test can compose
    /// operations onto one context.
    /// </summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The result of the add.</returns>
    public async Task<RepositoryResult<TestEntity>> AddUsingContextAsync(TestDbContext dbContext, TestEntity entity)
    {
        return await AddWithContextAsync(dbContext, entity);
    }
}

/// <summary>
/// A concrete repository for <see cref="TestUuidEntity"/>.
/// </summary>
public class TestUuidEntityRepository : RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>
{
    public TestUuidEntityRepository(
        ILogger<RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>> logger,
        IDatabaseContextProvider<TestDbContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}

/// <summary>
/// A concrete repository for <see cref="TestTemporalEntity"/>.
/// </summary>
public class TestTemporalEntityRepository : TemporalRepositoryBase<TestDbContext, long, TestTemporalEntity>
{
    public TestTemporalEntityRepository(
        ILogger<TemporalRepositoryBase<TestDbContext, long, TestTemporalEntity>> logger,
        IDatabaseContextProvider<TestDbContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}
