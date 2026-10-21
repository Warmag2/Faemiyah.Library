# Faemiyah.RepositoryPrototype

Entity Framework Core repository base classes for PostgreSQL.

What you get by deriving from them:

- **Keyset pagination** rather than `Skip`/`Take`, so a page costs the same however deep into the set it sits.
- **Optimistic concurrency** through PostgreSQL's `xmin` system column, maintained by the database, with no extra
  column in your tables.
- **A database per tenant or per isolated subsystem**, created and migrated on first use, from a single configured
  connection string.
- **A context per operation**, which is what makes the repositories safe to register as singletons.

## Getting started

Derive an entity, a context, a provider and a repository:

```csharp
public class Order : EntityBase<long>
{
    public string Reference { get; set; }
}

public class ShopContext : DbContext
{
    public ShopContext(DbContextOptions<ShopContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
}

public class ShopContextProvider : PgSqlDatabaseContextProvider<ShopContext>
{
    public ShopContextProvider(
        ILogger<PgSqlDatabaseContextProvider<ShopContext>> logger,
        IOptions<SqlOptions> sqlOptions,
        IConnectionStringAccessor connectionStringAccessor)
        : base(logger, sqlOptions, connectionStringAccessor)
    {
    }

    protected override ShopContext GetContextInstance(DbContextOptions<ShopContext> options)
        => new ShopContext(options);

    protected override async Task OnDatabaseProvisionedAsync(
        ShopContext dbContext,
        CancellationToken cancellationToken = default)
        => await dbContext.Database.MigrateAsync(cancellationToken);
}

public class OrderRepository : RepositoryBase<ShopContext, long, Order>
{
    public OrderRepository(
        ILogger<RepositoryBase<ShopContext, long, Order>> logger,
        IDatabaseContextProvider<ShopContext> databaseContextProvider)
        : base(logger, databaseContextProvider)
    {
    }
}
```

Register it:

```csharp
services.AddPgSqlRepositoryProvider<ShopContext, ShopContextProvider>(
    options => configuration.GetSection("Sql").Bind(options));

services.AddSingleton<IEntityRepository<long, Order>, OrderRepository>();
```

The base connection string is read from `ConnectionStrings:Database`. Its database name is replaced per call, so
the account it names needs `CREATEDB` if you rely on databases being created on demand.

## Things worth knowing before you rely on it

**Register repositories and the provider as singletons.** The provider holds the record of which databases have
been provisioned, so a shorter lifetime makes it repeat provisioning for every scope. Repositories create a context
per operation and hold no state, so one instance serves concurrent callers — that is the point of building them this
way, and a scoped registration gains nothing.

**Set `SqlOptions.MaxPoolSize` if you touch more than a couple of databases.** Npgsql keeps a separate pool per
connection string, and the provider default is 100. Two or three databases under load can exceed a stock PostgreSQL
`max_connections` of 100, at which point connections fail with `53300: sorry, too many clients already`. Keep
`MaxPoolSize` multiplied by the number of concurrently used databases comfortably below the server limit, or put a
connection pooler in front.

**Leave `SqlOptions.DefaultDbName` unset in a multi-database service.** With it set, a name that fails to resolve
silently falls back to the shared default instead of failing.

**Dates must be UTC.** `TemporalEntityBase` maps its dates to `timestamp with time zone`, and writing a `DateTime`
whose `Kind` is not `Utc` fails. `DateBegin` is also required: an entity without one is never matched by validity
queries, rather than being valid from the beginning of time.

**Validity is evaluated against an injectable clock.** `GetLatestValidAsync()` asks the repository's `TimeProvider`
for the current instant, and `GetLatestValidAtAsync(instant)` answers the same question for any other point in time.
Pass a `TimeProvider` to the repository constructor to control what "now" means; both period boundaries are
inclusive and compared at full timestamp precision.

**Round-trip `ConcurrencyToken` to get concurrency checking.** Read an entity, keep the token, pass it back on
update, and a conflicting write reports `OperationStatus.Conflict`. Leave it at its default and the check is
skipped, which is what lets a caller build an entity from a transport payload without participating.

**Writes report their outcome rather than throwing.** `AddAsync`, `UpdateAsync`, `AddOrUpdateAsync`, `GetOrAddAsync`
and `DeleteAsync` return a `RepositoryResult<T>` whose `Status` is one of:

| Status      | Meaning                                                                                   |
|-------------|-------------------------------------------------------------------------------------------|
| `Added`     | The entity was inserted.                                                                  |
| `Updated`   | The stored entity was overwritten.                                                        |
| `Found`     | `GetOrAddAsync` returned an existing entity, including the winner of a lost insert race.  |
| `Deleted`   | The entity, or every entity matching the predicate (possibly none), was deleted.          |
| `NotFound`  | Nothing matched the entity to update, or the unique ID to delete.                         |
| `Duplicate` | The duplication predicate matched, or a unique constraint rejected the insert or update.  |
| `Conflict`  | The stored entity was changed or removed by someone else meanwhile.                       |

`IsSuccess` is true for the first four, and then `Value` is non-null. Exceptions are reserved for programming errors
— a null or over-broad predicate, or an update that would overwrite one entity's unique ID with another's — and for
infrastructure failures. A derived repository composing its own saves should go through `SaveWithContextAsync`,
which maps failures the same way and clears the change tracker so a failed save is not retried by the next one.

```csharp
var result = await orders.UpdateAsync(order, o => o.Reference == order.Reference);

switch (result.Status)
{
    case OperationStatus.Updated:
        return Ok(result.Value);
    case OperationStatus.NotFound:
        return NotFound();
    case OperationStatus.Conflict:
        return Conflict(); // re-read and retry
    default:
        return UnprocessableEntity();
}
```

**Nullability is annotated.** Reads which return `null` to mean "not found" say so in their signatures, as does
`RepositoryResult<T>.Value`. Consumers compiled with `<Nullable>disable</Nullable>` are unaffected.

## Status

Version 0.1.0. The public API is not yet stable.
