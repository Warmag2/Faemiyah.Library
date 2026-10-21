using System;

namespace Faemiyah.Repository.Options;

/// <summary>
/// Configuration options for database connections and provisioning.
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// The database to use when no explicit name is supplied to the context provider.
    /// </summary>
    /// <remarks>
    /// Leave unset in a service which routes to several databases, so that an unresolved name fails loudly instead
    /// of silently falling back to a shared one.
    /// </remarks>
    public string? DefaultDbName { get; set; }

    /// <summary>
    /// The maintenance database used when creating a new database, or <b>null</b> for the provider default of
    /// <b>postgres</b>.
    /// </summary>
    /// <remarks>
    /// Only needed where that database is unreachable, as with some managed PostgreSQL offerings.
    /// </remarks>
    public string? AdminDbName { get; set; }

    /// <summary>
    /// How long to allow for creating a database and applying its migrations.
    /// </summary>
    public TimeSpan ProvisioningTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The maximum number of connections pooled per database, or <b>null</b> for the provider default of 100.
    /// </summary>
    /// <remarks>
    /// Npgsql pools per connection string, so a service multiplies this by the number of databases it touches within
    /// a pool's idle lifetime. Even a couple of databases at the default can exceed a stock PostgreSQL
    /// <b>max_connections</b> of 100, after which connections fail with <b>53300: sorry, too many clients already</b>.
    /// Keep the product below the server limit, or front the server with a pooler.
    /// </remarks>
    public int? MaxPoolSize { get; set; }

    /// <summary>
    /// Whether to let several connections share one physical connection.
    /// </summary>
    /// <remarks>
    /// Off by default, matching the provider. It raises throughput for many short queries, but a transaction pins
    /// the physical connection, so saving changes gains nothing. Measure before turning it on.
    /// </remarks>
    public bool Multiplexing { get; set; }
}
