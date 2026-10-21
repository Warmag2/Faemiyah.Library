using System.Text.Json;
using AwesomeAssertions;
using Faemiyah.Common.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Faemiyah.Common.Tests;

[TestFixture]
public class JsonConfigurationTests
{
    private enum Color
    {
        Red,
    }

    [Test]
    public void Defaults_WriteEnumsAsStringsAndSkipNulls()
    {
        JsonSerializer.Serialize(new Sample(Color.Red, null), JsonConfiguration.CreateJsonSerializerOptions()).Should().Be("""{"Color":"Red"}""");
    }

    [Test]
    public void Defaults_MatchPropertyNamesCaseSensitively()
    {
        JsonSerializer.Deserialize<Sample>("""{"color":"Red","Name":"x"}""", JsonConfiguration.CreateJsonSerializerOptions())!.Name.Should().Be("x");
        JsonSerializer.Deserialize<Sample>("""{"name":"x"}""", JsonConfiguration.CreateJsonSerializerOptions())!.Name.Should().BeNull();
    }

    [Test]
    public void ConfigureJsonSerializerOptions_AppliesTheDefaultsToTheInjectedOptions()
    {
        using var provider = new ServiceCollection().ConfigureJsonSerializerOptions().BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;

        options.PropertyNameCaseInsensitive.Should().BeFalse();
        options.Converters.Should().ContainSingle();
    }

    private sealed record Sample(Color Color, string? Name);
}
