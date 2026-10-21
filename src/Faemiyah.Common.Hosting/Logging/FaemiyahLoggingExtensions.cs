using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Faemiyah.Common.Hosting.Logging;

/// <summary>
/// Faemiyah console logging. Levels come from the usual <c>Logging:LogLevel</c> configuration in hosted programs, and
/// from <c>SetMinimumLevel</c> and <c>AddFilter</c> elsewhere:
/// <code>
/// builder.Logging.AddFaemiyahLogging();
/// using var factory = LoggerFactory.Create(logging => logging.AddFaemiyahLogging().SetMinimumLevel(LogLevel.Information));
/// </code>
/// </summary>
public static class FaemiyahLoggingExtensions
{
    /// <summary>
    /// Log only to the console, in the <see cref="FaemiyahConsoleFormatter"/> format.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <returns>The builder for further processing.</returns>
    public static ILoggingBuilder AddFaemiyahLogging(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ClearProviders();
        builder.AddConsoleFormatter<FaemiyahConsoleFormatter, ConsoleFormatterOptions>();
        builder.AddConsole(options => options.FormatterName = FaemiyahConsoleFormatter.FormatterName);
        return builder;
    }
}
