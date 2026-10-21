using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Faemiyah.Common;

/// <summary>
/// The Faemiyah default JSON settings: nulls are not written, property names are case-sensitive and enums are strings.
/// Use the same settings at every serialization boundary (Orleans, HTTP, SignalR, Redis, clients).
/// </summary>
public static class JsonConfiguration
{
    /// <summary>
    /// Build a new <see cref="JsonSerializerOptions"/> instance with the Faemiyah defaults applied.
    /// </summary>
    /// <returns>The configured options instance.</returns>
    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        ApplyDefaultJsonSerializerOptions(options);
        return options;
    }

    /// <summary>
    /// Apply the Faemiyah default JSON serializer settings to an existing <see cref="JsonSerializerOptions"/> instance.
    /// </summary>
    /// <param name="options">The options instance to mutate.</param>
    public static void ApplyDefaultJsonSerializerOptions(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNameCaseInsensitive = false;
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
