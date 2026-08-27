using StaySphere.Domain.Common;

namespace StaySphere.Tests.Domain;

/// <summary>
/// Smoke coverage for the one place date-overlap logic lives. The full suite
/// (domain, application, integration, E2E) is Stage 2.
/// </summary>
public class DateRangeTests
{
    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public void AdjacentRanges_DoNotOverlap()
    {
        var existing = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var requested = new DateRange(D("2026-09-13"), D("2026-09-15"));

        Assert.False(existing.OverlapsWith(requested));
        Assert.False(requested.OverlapsWith(existing));
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-13", "2026-09-10", "2026-09-13")] // exact overlap
    [InlineData("2026-09-10", "2026-09-13", "2026-09-12", "2026-09-15")] // partial overlap
    [InlineData("2026-09-10", "2026-09-20", "2026-09-12", "2026-09-15")] // existing contains requested
    [InlineData("2026-09-12", "2026-09-15", "2026-09-10", "2026-09-20")] // requested contains existing
    public void OverlappingScenarios_AreDetected(string start1, string end1, string start2, string end2)
    {
        var a = new DateRange(D(start1), D(end1));
        var b = new DateRange(D(start2), D(end2));

        Assert.True(a.OverlapsWith(b));
        Assert.True(b.OverlapsWith(a));
    }

    [Theory]
    [InlineData("2026-09-13", "2026-09-13")] // zero-night
    [InlineData("2026-09-15", "2026-09-13")] // negative
    public void InvalidRange_Throws(string start, string end)
    {
        Assert.Throws<BusinessRuleViolationException>(() => new DateRange(D(start), D(end)));
    }

    [Fact]
    public void Nights_CountsCalendarNights()
    {
        Assert.Equal(3, new DateRange(D("2026-09-10"), D("2026-09-13")).Nights);
    }
}
