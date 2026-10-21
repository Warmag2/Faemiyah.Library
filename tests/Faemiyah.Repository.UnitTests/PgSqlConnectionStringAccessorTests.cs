using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Faemiyah.Repository.Providers;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using MsOptions = Microsoft.Extensions.Options.Options;
using SqlOptions = Faemiyah.Repository.Options.SqlOptions;

namespace Faemiyah.Repository.UnitTests;

/// <summary>
/// Covers how the accessor reports a base connection string it cannot use.
/// </summary>
/// <remarks>
/// A missing or hostless connection string used to reach Npgsql unchecked, which reported it much later as an
/// <see cref="ArgumentNullException"/> naming <c>Host</c> -- and the broken result was cached, so every later call
/// repeated it. Nothing in that pointed at the configuration key actually at fault.
/// </remarks>
[TestFixture]
public class PgSqlConnectionStringAccessorTests
{
    [Test]
    public void GetConnectionString_NoConfiguredConnectionString_ThrowsNamingTheKey()
    {
        // Arrange
        var accessor = CreateAccessor(null);

        // Act
        var act = () => accessor.GetConnectionString("somedb");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionStrings:Database*");
    }

    [Test]
    public void GetConnectionString_ConnectionStringWithoutHost_Throws()
    {
        // Arrange
        var accessor = CreateAccessor("Username=postgres");

        // Act
        var act = () => accessor.GetConnectionString("somedb");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*no host*");
    }

    [Test]
    public void GetConnectionString_ValidConnectionString_TargetsTheRequestedDatabase()
    {
        // Arrange
        var accessor = CreateAccessor("Host=localhost;Username=postgres");

        // Act
        var connectionString = accessor.GetConnectionString("somedb");

        // Assert
        connectionString.Should().Contain("Database=somedb").And.Contain("Host=localhost");
    }

    private static PgSqlConnectionStringAccessor CreateAccessor(string? baseConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = baseConnectionString
            })
            .Build();

        return new PgSqlConnectionStringAccessor(configuration, MsOptions.Create(new SqlOptions()));
    }
}
