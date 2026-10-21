using System;
using System.IO;
using AwesomeAssertions;
using Faemiyah.Common.Hosting.Logging;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Faemiyah.Common.Tests;

/// <summary>
/// Message arguments must only be formatted when the entry passes the minimum level, so logging an expensive
/// argument below it costs nothing.
/// </summary>
[TestFixture]
public class FaemiyahLoggingTests
{
    private TextWriter _originalOut = null!;

    [SetUp]
    public void Setup()
    {
        _originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
    }

    [Test]
    public void Log_BelowTheMinimumLevel_DoesNotFormatTheArguments()
    {
        var counter = new ToStringCounter();
        using var factory = LoggerFactory.Create(logging => logging.AddFaemiyahLogging().SetMinimumLevel(LogLevel.Information));

        factory.CreateLogger("Test").LogDebug("Value: {Counter}", counter);

        counter.Invocations.Should().Be(0);
    }

    [Test]
    public void Log_AtTheMinimumLevel_FormatsTheArgumentsOnce()
    {
        var counter = new ToStringCounter();
        using var factory = LoggerFactory.Create(logging => logging.AddFaemiyahLogging().SetMinimumLevel(LogLevel.Information));

        factory.CreateLogger("Test").LogInformation("Value: {Counter}", counter);

        counter.Invocations.Should().Be(1);
    }

    private sealed class ToStringCounter
    {
        public int Invocations { get; private set; }

        public override string ToString()
        {
            Invocations++;
            return "counted";
        }
    }
}
