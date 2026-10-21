using System;
using System.Collections.Concurrent;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Faemiyah.Repository.Providers;

/// <summary>
/// Singleton accessor for cached connection strings.
/// </summary>
public class PgSqlConnectionStringAccessor : IConnectionStringAccessor
{
    /// <summary>
    /// The configured connection string the per-database ones are derived from.
    /// </summary>
    private const string ConnectionStringName = "Database";

    private readonly IConfiguration _configuration;
    private readonly SqlOptions _sqlOptions;
    private readonly ConcurrentDictionary<string, string> _connectionStrings = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlConnectionStringAccessor"/> class.
    /// </summary>
    /// <param name="configuration">The configuration holding the base connection string.</param>
    /// <param name="sqlOptions">The database options.</param>
    public PgSqlConnectionStringAccessor(IConfiguration configuration, IOptions<SqlOptions> sqlOptions)
    {
        _configuration = configuration;
        _sqlOptions = sqlOptions.Value;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <c>ConnectionStrings:Database</c> is configured, or when the configured one names no host.
    /// </exception>
    public string GetConnectionString(string dbName)
    {
        return _connectionStrings.GetOrAdd(dbName, BuildConnectionString);
    }

    private string BuildConnectionString(string dbName)
    {
        var baseConnectionString = _configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                $"No connection string named '{ConnectionStringName}' is configured. Add it under "
                + $"'ConnectionStrings:{ConnectionStringName}'.");
        }

        // Pooling is left at the provider default of on, which a context-per-operation design depends on.
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = dbName,
            Multiplexing = _sqlOptions.Multiplexing
        };

        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host))
        {
            throw new InvalidOperationException(
                $"The connection string named '{ConnectionStringName}' specifies no host.");
        }

        if (_sqlOptions.MaxPoolSize.HasValue)
        {
            connectionStringBuilder.MaxPoolSize = _sqlOptions.MaxPoolSize.Value;
        }

        return connectionStringBuilder.ToString();
    }
}
