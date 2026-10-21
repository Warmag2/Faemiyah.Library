using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Faemiyah.Common.Hosting;

/// <summary>
/// Registration of the <see cref="JsonConfiguration"/> defaults.
/// </summary>
public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Apply the Faemiyah JSON defaults to the injectable <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection ConfigureJsonSerializerOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Configure<JsonSerializerOptions>(JsonConfiguration.ApplyDefaultJsonSerializerOptions);
    }
}
