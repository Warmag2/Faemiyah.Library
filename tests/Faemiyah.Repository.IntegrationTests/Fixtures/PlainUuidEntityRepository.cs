using System;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Repositories;
using Microsoft.Extensions.Logging;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A plain repository over <see cref="TestUuidEntity"/>, without the identity check of the unique ID base.
/// </summary>
/// <remarks>
/// The unique ID base refuses to write one unique ID over another, so an update can never reach the unique index
/// through it. This repository can, which is what lets a test drive an update into a unique constraint violation.
/// </remarks>
public class PlainUuidEntityRepository : RepositoryBase<TestDbContext, long, TestUuidEntity>
{
    public PlainUuidEntityRepository(
        ILogger<RepositoryBase<TestDbContext, long, TestUuidEntity>> logger,
        IDatabaseContextProvider<TestDbContext> databaseContextProvider,
        TimeProvider? timeProvider = null)
        : base(logger, databaseContextProvider, timeProvider)
    {
    }
}
