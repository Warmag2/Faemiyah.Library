using System.Collections.Generic;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A plain entity, used to exercise <see cref="Repositories.RepositoryBase{TContext, TKey, TEntity}"/>.
/// </summary>
public class TestEntity : EntityBase<long>
{
    public string? Name { get; set; }

    public int Value { get; set; }

    public ICollection<TestChildEntity>? Children { get; set; }
}

/// <summary>
/// A related entity, so that include processing has something to include.
/// </summary>
public class TestChildEntity : EntityBase<long>
{
    public string? Label { get; set; }

    public long TestEntityId { get; set; }

    public TestEntity? TestEntity { get; set; }
}

/// <summary>
/// An entity carrying a public identifier, used to exercise
/// <see cref="Repositories.RepositoryBaseWithUuid{TContext, TKey, TEntity}"/>.
/// </summary>
public class TestUuidEntity : EntityBaseWithUuid<long>
{
    public string? Name { get; set; }

    public int Value { get; set; }
}

/// <summary>
/// An entity with a validity period, used to exercise
/// <see cref="Repositories.TemporalRepositoryBase{TContext, TKey, TEntity}"/>.
/// </summary>
public class TestTemporalEntity : TemporalEntityBase<long>
{
    public string? Name { get; set; }
}
