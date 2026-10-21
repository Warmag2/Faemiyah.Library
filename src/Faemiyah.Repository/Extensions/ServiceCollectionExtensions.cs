using System;
using System.Linq;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Options;
using Faemiyah.Repository.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Faemiyah.Repository.Extensions;

/// <summary>
/// Registration helpers for wiring the repository infrastructure into a service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL database context provider together with everything it depends on.
    /// </summary>
    /// <remarks>
    /// Both registrations must stay singletons: the provider holds the record of which databases are provisioned, so
    /// a shorter lifetime would repeat provisioning per scope. <c>TryAdd</c> means calling this once per context type
    /// shares one accessor.
    /// <para>
    /// <see cref="SqlOptions"/> is process-wide rather than per context type. Calling this for a second context type
    /// is supported, but every call must configure the options identically, and a call which does not throws. Route
    /// a context type to another database by passing the name to
    /// <see cref="IDatabaseContextProvider{TContext}.GetDbContextAsync"/> rather than by changing
    /// <see cref="SqlOptions.DefaultDbName"/>.
    /// </para>
    /// <para>
    /// Register repositories yourself, and register them as singletons too. They hold no state and create a context
    /// per operation, so one instance serves concurrent callers; the usual reason to scope a repository is a scoped
    /// context, which these do not have.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The type of the database context.</typeparam>
    /// <typeparam name="TProvider">The concrete provider supplying instances of that context.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Configures the database options.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <b>null</b>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddPgSqlRepositoryProvider&lt;MyContext, MyContextProvider&gt;(
    ///     options => configuration.GetSection("Sql").Bind(options));
    ///
    /// services.AddSingleton&lt;IEntityRepository&lt;long, Order&gt;, OrderRepository&gt;();
    /// services.AddSingleton&lt;ITemporalEntityRepository&lt;long, Price&gt;, PriceRepository&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddPgSqlRepositoryProvider<TContext, TProvider>(
        this IServiceCollection services,
        Action<SqlOptions> configureOptions)
        where TContext : DbContext
        where TProvider : PgSqlDatabaseContextProvider<TContext>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        RequireConsistentOptions(services, configureOptions);

        services.Configure(configureOptions);
        services.TryAddSingleton<IConnectionStringAccessor, PgSqlConnectionStringAccessor>();
        services.TryAddSingleton<IDatabaseContextProvider<TContext>, TProvider>();

        return services;
    }

    /// <summary>
    /// Rejects a second registration which asks for different options than the first.
    /// </summary>
    /// <remarks>
    /// <see cref="SqlOptions"/> is process-wide: the options are bound once and every provider and the shared
    /// connection string accessor read the same instance. Registering a second context type is supported and is the
    /// reason the accessor is shared, but because
    /// <see cref="OptionsServiceCollectionExtensions.Configure{TOptions}(IServiceCollection, Action{TOptions})"/>
    /// applies every configure action to that one instance, two differing registrations would otherwise merge
    /// per-property into a combination neither caller asked for -- silently, and including
    /// <see cref="SqlOptions.DefaultDbName"/>, which selects the database. Failing at registration turns that into
    /// an error at startup instead of a connection to the wrong database at runtime.
    /// <para>
    /// The action is invoked against a throwaway instance to compare the result, so it must be free of side effects.
    /// Binding a configuration section, as intended, is.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration this call is asking for.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a previous call configured different options.
    /// </exception>
    private static void RequireConsistentOptions(IServiceCollection services, Action<SqlOptions> configureOptions)
    {
        var requested = new SqlOptions();

        configureOptions(requested);

        var marker = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(SqlOptionsRegistration))?
            .ImplementationInstance as SqlOptionsRegistration;

        if (marker == null)
        {
            services.AddSingleton(new SqlOptionsRegistration(requested));

            return;
        }

        var existing = marker.Options;

        if (existing.DefaultDbName == requested.DefaultDbName
            && existing.AdminDbName == requested.AdminDbName
            && existing.ProvisioningTimeout == requested.ProvisioningTimeout
            && existing.MaxPoolSize == requested.MaxPoolSize
            && existing.Multiplexing == requested.Multiplexing)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{nameof(AddPgSqlRepositoryProvider)} has already been called with different {nameof(SqlOptions)}. "
            + $"The options are process-wide, so every call must configure them identically. "
            + $"Previously configured {nameof(SqlOptions.DefaultDbName)} '{existing.DefaultDbName}', "
            + $"now '{requested.DefaultDbName}'. To route one context type to another database, pass the name to "
            + $"{nameof(IDatabaseContextProvider<DbContext>.GetDbContextAsync)} instead of changing the default.");
    }

    /// <summary>
    /// Records the options the first registration asked for, so later ones can be checked against them.
    /// </summary>
    /// <param name="Options">The options as configured by the first call.</param>
    private sealed record SqlOptionsRegistration(SqlOptions Options);
}
