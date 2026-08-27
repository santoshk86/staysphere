using StaySphere.Domain.Common;

namespace StaySphere.Tests.Domain;

/// <summary>
/// The half-open interval <c>[Start, End)</c> and its overlap rule are the single
/// most important piece of business logic in the system, so this is the most
/// thorough file in the suite.
/// </summary>
public class DateRangeTests
{
    private static DateOnly D(string value) => DateOnly.Parse(value);

    // ---- construction / validity -------------------------------------------------

    [Fact]
    public void Constructor_AcceptsValidRange_AndReportsNights()
    {
        var range = new DateRange(D("2026-09-10"), D("2026-09-13"));

        Assert.Equal(D("2026-09-10"), range.Start);
        Assert.Equal(D("2026-09-13"), range.End);
        Assert.Equal(3, range.Nights);
    }

    [Fact]
    public void Constructor_Throws_WhenCheckOutEqualsCheckIn()
    {
        Assert.Throws<BusinessRuleViolationException>(
            () => new DateRange(D("2026-09-13"), D("2026-09-13")));
    }

    [Fact]
    public void Constructor_Throws_WhenCheckOutBeforeCheckIn()
    {
        Assert.Throws<BusinessRuleViolationException>(
            () => new DateRange(D("2026-09-15"), D("2026-09-13")));
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-11", 1)]
    [InlineData("2026-09-10", "2026-09-13", 3)]
    [InlineData("2026-09-10", "2026-10-10", 30)]
    public void Nights_CountsCalendarNights(string start, string end, int expectedNights)
    {
        Assert.Equal(expectedNights, new DateRange(D(start), D(end)).Nights);
    }

    // ---- overlap matrix --------------------------------------------------------

    // Reference existing stay for every row: 2026-09-10 -> 2026-09-13.
    [Theory]
    // exact same range
    [InlineData("2026-09-10", "2026-09-13", true)]
    // overlap at the beginning of the existing stay
    [InlineData("2026-09-08", "2026-09-11", true)]
    // overlap at the end of the existing stay
    [InlineData("2026-09-12", "2026-09-15", true)]
    // requested range completely contains the existing stay
    [InlineData("2026-09-08", "2026-09-20", true)]
    // requested range sits completely inside the existing stay
    [InlineData("2026-09-11", "2026-09-12", true)]
    // no overlap – entirely before, not touching
    [InlineData("2026-09-05", "2026-09-08", false)]
    // no overlap – entirely after, not touching
    [InlineData("2026-09-16", "2026-09-18", false)]
    // adjacent – requested ends exactly when existing starts
    [InlineData("2026-09-08", "2026-09-10", false)]
    // adjacent – requested starts exactly when existing ends
    [InlineData("2026-09-13", "2026-09-15", false)]
    public void OverlapsWith_AppliesHalfOpenRule(string requestedStart, string requestedEnd, bool expected)
    {
        var existing = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var requested = new DateRange(D(requestedStart), D(requestedEnd));

        Assert.Equal(expected, existing.OverlapsWith(requested));
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-13", "2026-09-12", "2026-09-15", true)]
    [InlineData("2026-09-10", "2026-09-13", "2026-09-13", "2026-09-15", false)]
    [InlineData("2026-09-10", "2026-09-20", "2026-09-12", "2026-09-15", true)]
    [InlineData("2026-09-12", "2026-09-15", "2026-09-10", "2026-09-20", true)]
    [InlineData("2026-09-10", "2026-09-13", "2026-09-08", "2026-09-10", false)]
    public void OverlapsWith_IsSymmetric(string aStart, string aEnd, string bStart, string bEnd, bool expected)
    {
        var a = new DateRange(D(aStart), D(aEnd));
        var b = new DateRange(D(bStart), D(bEnd));

        Assert.Equal(expected, a.OverlapsWith(b));
        Assert.Equal(expected, b.OverlapsWith(a));
    }

    [Fact]
    public void OverlapsWith_ExampleFromRequirements_AdjacentIsAvailable()
    {
        var existing = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var requested = new DateRange(D("2026-09-13"), D("2026-09-15"));

        Assert.False(existing.OverlapsWith(requested));
    }

    [Fact]
    public void OverlapsWith_ExampleFromRequirements_PartialIsConflict()
    {
        var existing = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var requested = new DateRange(D("2026-09-12"), D("2026-09-15"));

        Assert.True(existing.OverlapsWith(requested));
    }

    [Fact]
    public void OverlapsWith_Throws_WhenOtherIsNull()
    {
        var range = new DateRange(D("2026-09-10"), D("2026-09-13"));

        Assert.Throws<ArgumentNullException>(() => range.OverlapsWith(null!));
    }

    // ---- value semantics -----------------------------------------------------

    [Fact]
    public void Equality_IsByValue()
    {
        var a = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var b = new DateRange(D("2026-09-10"), D("2026-09-13"));
        var different = new DateRange(D("2026-09-10"), D("2026-09-14"));

        Assert.Equal(a, b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, different);
        Assert.False(a.Equals((object?)null));
    }

    [Fact]
    public void ToString_ShowsTheHalfOpenInterval()
    {
        var range = new DateRange(D("2026-09-10"), D("2026-09-13"));

        Assert.Equal("[2026-09-10, 2026-09-13)", range.ToString());
    }
}
