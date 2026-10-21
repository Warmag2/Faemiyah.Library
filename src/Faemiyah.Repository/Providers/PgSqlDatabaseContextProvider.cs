using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Faemiyah.Repository.Providers;

/// <inheritdoc />
public abstract class PgSqlDatabaseContextProvider<TContext> : IDatabaseContextProvider<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// The provisioning task for each database, keyed by database name.
    /// </summary>
    /// <remarks>
    /// The <see cref="Lazy{T}"/> is what guarantees provisioning runs once per database.
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,Func{TKey,TValue})"/> alone does not, because it
    /// may invoke its factory on several threads and discard the losers.
    /// </remarks>
    private readonly ConcurrentDictionary<string, Lazy<Task>> _provisioning = new();

    private readonly ILogger _logger;
    private readonly SqlOptions _sqlOptions;
    private readonly IConnectionStringAccessor _connectionStringAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlDatabaseContextProvider{TContext}"/> class.
    /// </summary>
    /// <param name="logger">The logging interface.</param>
    /// <param name="sqlOptions">The database options.</param>
    /// <param name="connectionStringAccessor">The connection string accessor.</param>
    protected PgSqlDatabaseContextProvider(
        ILogger<PgSqlDatabaseContextProvider<TContext>> logger,
        IOptions<SqlOptions> sqlOptions,
        IConnectionStringAccessor connectionStringAccessor)
    {
        _logger = logger;
        _connectionStringAccessor = connectionStringAccessor;
        _sqlOptions = sqlOptions.Value;
    }

    /// <inheritdoc />
    public async Task<TContext> GetDbContextAsync(string? dbName = null, CancellationToken cancellationToken = default)
    {
        var resolvedDbName = ResolveDbName(dbName);
        var connectionString = _connectionStringAccessor.GetConnectionString(resolvedDbName);

        await ProvisionOnceAsync(resolvedDbName, connectionString, cancellationToken);

        return GetContextInstance(GetContextOptions(connectionString));
    }

    /// <summary>
    /// Construct the context this provider is for.
    /// </summary>
    /// <param name="dbContextOptions">The db context options.</param>
    /// <returns>The database context.</returns>
    protected abstract TContext GetContextInstance(DbContextOptions<TContext> dbContextOptions);

    /// <summary>
    /// Called once per database, after it has been confirmed to exist. Override to migrate or seed.
    /// </summary>
    /// <remarks>
    /// Every caller wanting a context for that database waits for this to finish. The context supplied has
    /// <see cref="SqlOptions.ProvisioningTimeout"/> as its command timeout.
    /// </remarks>
    /// <param name="dbContext">The database context to provision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <example>
    /// <code>
    /// protected override async Task OnDatabaseProvisionedAsync(MyContext dbContext, CancellationToken cancellationToken = default)
    /// {
    ///     await dbContext.Database.MigrateAsync(cancellationToken);
    /// }
    /// </code>
    /// </example>
    protected virtual Task OnDatabaseProvisionedAsync(TContext dbContext, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensure the database targeted by the given connection string exists, creating it if it does not.
    /// </summary>
    /// <param name="connectionString">The connection string of the target database.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected virtual async Task EnsureDatabaseExistsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new DbContext(GetDbCreationContextOptions(connectionString));
        var creator = dbContext.GetService<IRelationalDatabaseCreator>();

        if (await creator.ExistsAsync(cancellationToken))
        {
            return;
        }

        try
        {
            await creator.CreateAsync(cancellationToken);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            _logger.LogInformation(
                e,
                "Database {DbName} was created concurrently by another process.",
                GetDbNameFromConnectionString(connectionString));
        }
    }

    private static string? GetDbNameFromConnectionString(string connectionString)
    {
        return new NpgsqlConnectionStringBuilder(connectionString).Database;
    }

    private string ResolveDbName(string? dbName)
    {
        var resolvedDbName = string.IsNullOrWhiteSpace(dbName) ? _sqlOptions.DefaultDbName : dbName;

        if (string.IsNullOrWhiteSpace(resolvedDbName))
        {
            throw new InvalidOperationException(
                $"No database name was supplied and no {nameof(SqlOptions)}.{nameof(SqlOptions.DefaultDbName)} is configured.");
        }

        return resolvedDbName;
    }

    private async Task ProvisionOnceAsync(string dbName, string connectionString, CancellationToken cancellationToken)
    {
        var lazy = _provisioning.GetOrAdd(
            dbName,
            name => new Lazy<Task>(
                // The task is shared, so it must not be cancellable by whichever caller happens to create it.
                () => ProvisionAsync(name, connectionString, CancellationToken.None),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            // Lets this caller abandon its wait without cancelling the shared work.
            await lazy.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (lazy.Value.IsFaulted)
            {
                // A faulted task would stay cached, permanently poisoning this database.
                _provisioning.TryRemove(new KeyValuePair<string, Lazy<Task>>(dbName, lazy));
            }
        }
    }

    private async Task ProvisionAsync(string dbName, string connectionString, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning database {DbName}.", dbName);

        await EnsureDatabaseExistsAsync(connectionString, cancellationToken);

        await using var dbContext = GetContextInstance(GetProvisioningContextOptions(connectionString));

        await OnDatabaseProvisionedAsync(dbContext, cancellationToken);
    }

    /// <summary>
    /// Get the context options used for creating a database.
    /// </summary>
    /// <param name="connectionString">The connection string of the target database.</param>
    /// <returns>The database context options.</returns>
    private DbContextOptions<DbContext> GetDbCreationContextOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(
                connectionString,
                opts =>
                {
                    opts.CommandTimeout((int)_sqlOptions.ProvisioningTimeout.TotalSeconds);

                    if (!string.IsNullOrWhiteSpace(_sqlOptions.AdminDbName))
                    {
                        opts.UseAdminDatabase(_sqlOptions.AdminDbName);
                    }
                })
            .Options;
    }

    /// <summary>
    /// Get the context options used while provisioning a database.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <returns>The database context options.</returns>
    private DbContextOptions<TContext> GetProvisioningContextOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                connectionString,
                opts =>
                {
                    opts.CommandTimeout((int)_sqlOptions.ProvisioningTimeout.TotalSeconds);
                    ConfigureRetries(opts);
                })
            .Options;
    }

    /// <summary>
    /// Get the context options for the database.
    /// </summary>
    /// <param name="connectionString">The connection string to use.</param>
    /// <returns>The database context options.</returns>
    private DbContextOptions<TContext> GetContextOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString, ConfigureRetries)
            .Options;
    }

    /// <summary>
    /// Configure how transient failures are retried.
    /// </summary>
    /// <remarks>
    /// Uses the provider's defaults, which encode how PostgreSQL fails. Override where a deployment needs different
    /// behaviour: retry count multiplied by backoff sets the worst case latency of every operation, which is a
    /// decision about the service rather than the database.
    /// </remarks>
    /// <param name="opts">The provider specific options builder.</param>
    protected virtual void ConfigureRetries(NpgsqlDbContextOptionsBuilder opts)
    {
        opts.EnableRetryOnFailure();
    }
}
