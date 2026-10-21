using System;
using System.IO;
using AwesomeAssertions;
using Faemiyah.Common.Hosting.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Faemiyah.Common.Tests;

[TestFixture]
public class FaemiyahConsoleFormatterTests
{
    private static readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 10, 20, 10, 48, 53, 541, TimeSpan.FromHours(3)));

    [Test]
    public void Write_ProducesTimeLevelCategoryAndMessage()
    {
        Write(LogLevel.Warning, "Disk C is full", null).Should().Be($"2026-10-20T07:48:53.541Z - [Warning] - Test.Category - Disk C is full{Environment.NewLine}");
    }

    [Test]
    public void Write_AppendsTheExceptionOnTheFollowingLines()
    {
        var exception = new InvalidOperationException("Broken");

        Write(LogLevel.Error, "Failed", exception).Should().StartWith($"2026-10-20T07:48:53.541Z - [Error] - Test.Category - Failed{Environment.NewLine}System.InvalidOperationException: Broken");
    }

    [Test]
    public void Write_SkipsEmptyMessagesWithoutAnException()
    {
        Write(LogLevel.Information, string.Empty, null).Should().BeEmpty();
    }

    private static string Write(LogLevel level, string message, Exception? exception)
    {
        using var writer = new StringWriter();
        var entry = new LogEntry<string>(level, "Test.Category", default, message, exception, (s, _) => s);
        new FaemiyahConsoleFormatter(Time).Write(entry, null, writer);
        return writer.ToString();
    }
}
