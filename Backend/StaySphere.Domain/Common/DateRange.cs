namespace StaySphere.Domain.Common;

/// <summary>
/// A half-open date interval <c>[Start, End)</c> used for reservation stays.
/// Check-in is included; check-out is excluded, so a stay that checks out on the
/// same day another checks in does NOT overlap.
/// All date-overlap logic lives here so it is defined exactly once.
/// </summary>
public sealed class DateRange : IEquatable<DateRange>
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end <= start)
        {
            throw new BusinessRuleViolationException(
                $"Check-out ({end:yyyy-MM-dd}) must be after check-in ({start:yyyy-MM-dd}).");
        }

        Start = start;
        End = end;
    }

    /// <summary>Number of calendar nights in the stay.</summary>
    public int Nights => End.DayNumber - Start.DayNumber;

    /// <summary>
    /// True when this interval and <paramref name="other"/> share at least one night.
    /// Adjacent intervals (one ends where the other starts) do not overlap.
    /// </summary>
    public bool OverlapsWith(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    public bool Equals(DateRange? other) => other is not null && Start == other.Start && End == other.End;

    public override bool Equals(object? obj) => Equals(obj as DateRange);

    public override int GetHashCode() => HashCode.Combine(Start, End);

    public override string ToString() => $"[{Start:yyyy-MM-dd}, {End:yyyy-MM-dd})";
}
