using StaySphere.Domain;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Tiny factory helpers for pure domain unit tests (no database). Every argument
/// has a sensible default so a test only states the value it cares about.
/// </summary>
public static class Build
{
    public static RoomType RoomType(
        string name = "Test Room Type",
        string description = "A room type used in tests.",
        decimal pricePerNight = 100.00m,
        int maxGuests = 2,
        string imageUrl = "/images/test.svg")
        => new(name, description, pricePerNight, maxGuests, imageUrl);

    public static Room Room(RoomType? roomType = null, string roomNumber = "101")
        => new(roomNumber, roomType ?? RoomType());

    public static DateOnly Sep(int day) => new(2026, 9, day);
}
