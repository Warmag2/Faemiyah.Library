using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Faemiyah.Repository.Interfaces;

/// <summary>
/// Interface for a provider that gives a database context for a given database name.
/// </summary>
/// <typeparam name="TContext">The type of the database context.</typeparam>
public interface IDatabaseContextProvider<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Gives a database context for the given database name, ensuring first that the database exists and
    /// has been provisioned.
    /// </summary>
    /// <param name="dbName">The name of the database, or <b>null</b> to use the configured default database.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The database context. The caller takes ownership of the returned context and is responsible for disposing it.
    /// </returns>
    Task<TContext> GetDbContextAsync(string? dbName = null, CancellationToken cancellationToken = default);
}
