# Faemiyah.Common

Defaults shared by Faemiyah programs, in two packages:

- **Faemiyah.Common**: no dependencies, so it is usable from Blazor WebAssembly too.
  - `JsonConfiguration`: the JSON serializer defaults. Nulls are not written, property names are case-sensitive, and
    enums are written as strings. Use them at every serialization boundary (Orleans, HTTP, SignalR, Redis, clients).
- **Faemiyah.Common.Hosting**: server-side setup.
  - `AddFaemiyahLogging()`: logs only to the console, one line per entry, with any exception on the following lines:
    `2026-10-20T07:48:53.541Z - [Warning] - Some.Category - The message`.
  - `ConfigureJsonSerializerOptions()`: applies the JSON defaults to the injectable `JsonSerializerOptions`.
  - `HostNetwork.GetHostIp()`: the host's first IPv4 address, for example to advertise an Orleans silo.

## Logging

In a hosted program, levels come from the usual `Logging:LogLevel` configuration:

```csharp
builder.Logging.AddFaemiyahLogging();
```

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Orleans": "Warning"
    }
  }
}
```

In a program without a host, such as a command-line tool:

```csharp
using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddFaemiyahLogging()
    .SetMinimumLevel(LogLevel.Information)
    .AddFilter("Microsoft", LogLevel.Warning));
```

## JSON

```csharp
var options = JsonConfiguration.CreateJsonSerializerOptions();
services.ConfigureJsonSerializerOptions();
services.ConfigureHttpJsonOptions(o => JsonConfiguration.ApplyDefaultJsonSerializerOptions(o.SerializerOptions));
```
