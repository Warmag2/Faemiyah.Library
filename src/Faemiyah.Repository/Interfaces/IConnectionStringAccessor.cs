namespace Faemiyah.Repository.Interfaces;

/// <summary>
/// Interface for accessing connection strings.
/// </summary>
public interface IConnectionStringAccessor
{
    /// <summary>
    /// Gets the connection string targeting the given database.
    /// </summary>
    /// <param name="dbName">The database name.</param>
    /// <returns>The connection string.</returns>
    string GetConnectionString(string dbName);
}
