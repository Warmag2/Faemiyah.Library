using System.Threading;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;
using Faemiyah.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A repository which reports when it recovers from a unique constraint violation.
/// </summary>
/// <remarks>
/// The recovery path taken when an insert loses a race is otherwise invisible from outside: it returns the same
/// <see cref="OperationStatus.Duplicate"/> the pre-existence check returns. Counting how
/// often the classifier accepts a failure gives a test a way to tell that the path actually ran, without asserting
/// on log output.
/// </remarks>
internal sealed class RaceObservingRepository : RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>
{
    private int _uniqueViolationsHandled;

    public RaceObservingRepository(
        ILogger<RepositoryBaseWithUuid<TestDbContext, long, TestUuidEntity>> logger,
        IDatabaseContextProvider<TestDbContext> databaseContextProvider)
        : base(logger, databaseContextProvider)
    {
    }

    /// <summary>
    /// How many saves failed on a unique constraint and were recovered from.
    /// </summary>
    public int UniqueViolationsHandled
    {
        get
        {
            return Volatile.Read(ref _uniqueViolationsHandled);
        }
    }

    /// <summary>
    /// Runs the library's own classifier against a given failure, without counting it.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>Whatever the base implementation decides.</returns>
    public bool ClassifyWithoutCounting(DbUpdateException exception)
    {
        return base.IsUniqueConstraintViolation(exception);
    }

    /// <remarks>
    /// This sits in an exception filter, so a call which returns <b>true</b> is followed by the recovery block
    /// running. Counting those calls therefore counts recoveries.
    /// </remarks>
    protected override bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var isUniqueViolation = base.IsUniqueConstraintViolation(exception);

        if (isUniqueViolation)
        {
            Interlocked.Increment(ref _uniqueViolationsHandled);
        }

        return isUniqueViolation;
    }
}
