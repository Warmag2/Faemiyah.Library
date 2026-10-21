using Microsoft.EntityFrameworkCore;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// The context under test. Deliberately configures nothing beyond the sets, so that whatever the library's
/// attributes and conventions produce is what gets exercised.
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    // Expression-bodied rather than auto-properties, so that the sets need no null-forgiving initialiser.
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    public DbSet<TestChildEntity> TestChildEntities => Set<TestChildEntity>();

    public DbSet<TestUuidEntity> TestUuidEntities => Set<TestUuidEntity>();

    public DbSet<TestTemporalEntity> TestTemporalEntities => Set<TestTemporalEntity>();
}
