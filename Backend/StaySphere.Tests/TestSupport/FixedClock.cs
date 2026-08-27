using StaySphere.Application.Common;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Deterministic <see cref="IClock"/> for tests. Time never advances unless a test
/// sets <see cref="UtcNow"/> explicitly, so "past date" / "today" rules are stable.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public FixedClock(DateOnly today)
        : this(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
    {
    }

    public DateTimeOffset UtcNow { get; set; }

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
