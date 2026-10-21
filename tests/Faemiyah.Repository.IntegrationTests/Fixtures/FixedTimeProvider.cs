using System;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// A clock which reports whatever instant it is told to.
/// </summary>
/// <remarks>
/// Hand written rather than taken from <c>Microsoft.Extensions.TimeProvider.Testing</c>, because overriding one
/// method is cheaper than another package reference.
/// </remarks>
internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="by">How far to move.</param>
    public void Advance(TimeSpan by)
    {
        _utcNow = _utcNow.Add(by);
    }
}
