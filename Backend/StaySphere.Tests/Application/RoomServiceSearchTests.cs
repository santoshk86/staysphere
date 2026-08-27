using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Common;
using StaySphere.Application.Rooms;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Application;

/// <summary>
/// Behaviour of the "search available rooms" use case against a real SQLite
/// database with the shipped catalog seed. These tests prove the capacity filter
/// and the half-open availability predicate actually run in SQL and return the
/// right rooms.
/// </summary>
public sealed class RoomServiceSearchTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private RoomService NewService() => new(_database.CreateContext(), NullLogger<RoomService>.Instance);

    private static RoomSearchQuery Query(DateOnly checkIn, DateOnly checkOut, int guests)
        => new(checkIn, checkOut, guests);

    // ---- happy path / capacity ------------------------------------------------

    [Fact]
    public async Task Search_ReturnsRooms_WhenNothingIsBooked()
    {
        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.Equal(SeededCatalog.RoomCount, rooms.Count);
    }

    [Fact]
    public async Task Search_ExcludesRoom_WhenCapacityIsLessThanGuestCount()
    {
        // Standard Queen / Deluxe King sleep 2; Executive sleeps 3; only Family Suite sleeps 4.
        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 4));

        Assert.All(rooms, room => Assert.True(room.MaxGuests >= 4));
        Assert.Equal(new[] { "301", "302" }, rooms.Select(r => r.RoomNumber));
    }

    [Fact]
    public async Task Search_IncludesRoom_WhenCapacityExactlyMeetsGuestCount()
    {
        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.Contains(rooms, r => r.RoomNumber == "101" && r.MaxGuests == 2);
    }

    [Fact]
    public async Task Search_ReturnsRoomsOrderedByPriceThenRoomNumber()
    {
        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.Equal(SeededCatalog.RoomNumbersByPriceThenNumber, rooms.Select(r => r.RoomNumber));
    }

    [Fact]
    public async Task Search_ProjectsRoomDetails_IncludingAlphabeticalAmenities()
    {
        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        var standardQueen = rooms.Single(r => r.RoomNumber == "101");
        Assert.Equal("Standard Queen", standardQueen.RoomType);
        Assert.Equal(SeededCatalog.StandardQueenPrice, standardQueen.PricePerNight);
        Assert.Equal(new[] { "Air conditioning", "Flat-screen TV", "Free Wi-Fi" }, standardQueen.Amenities);
    }

    // ---- availability: overlap excludes, adjacency does not -------------------

    [Fact]
    public async Task Search_ExcludesRoom_WhenAConfirmedReservationOverlapsTheRange()
    {
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(11), Build.Sep(14));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(12), Build.Sep(15), 2));

        Assert.DoesNotContain(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_IncludesRoom_WhenExistingReservationEndsOnCheckInDate()
    {
        // Existing 09-10 -> 09-13, requested 09-13 -> 09-15 : adjacent, so available.
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(10), Build.Sep(13));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(13), Build.Sep(15), 2));

        Assert.Contains(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_IncludesRoom_WhenExistingReservationStartsOnCheckOutDate()
    {
        // Existing 09-13 -> 09-16, requested 09-10 -> 09-13 : adjacent, so available.
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(13), Build.Sep(16));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.Contains(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_ExcludesRoom_WhenRequestedRangeContainsTheExistingReservation()
    {
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(12), Build.Sep(14));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(20), 2));

        Assert.DoesNotContain(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_ExcludesRoom_WhenExistingReservationContainsTheRequestedRange()
    {
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(10), Build.Sep(20));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(12), Build.Sep(15), 2));

        Assert.DoesNotContain(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_OnlyExcludesTheBookedRoom_NotOtherRoomsOfTheSameType()
    {
        await new ReservationSeeder(_database.CreateContext())
            .AddConfirmedAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(10), Build.Sep(13));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.DoesNotContain(rooms, r => r.RoomNumber == "101");
        Assert.Contains(rooms, r => r.RoomNumber == "102");
        Assert.Contains(rooms, r => r.RoomNumber == "103");
    }

    [Fact]
    public async Task Search_IncludesRoom_WhenTheOnlyOverlappingReservationIsCancelled()
    {
        await new ReservationSeeder(_database.CreateContext())
            .AddCancelledAsync(SeededCatalog.StandardQueenRoom101Id, Build.Sep(10), Build.Sep(13));

        var rooms = await NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), 2));

        Assert.Contains(rooms, r => r.RoomNumber == "101");
    }

    [Fact]
    public async Task Search_ReturnsEmpty_WhenEveryCapableRoomIsBookedForTheRange()
    {
        var seeder = new ReservationSeeder(_database.CreateContext());
        foreach (var roomId in new[] { SeededCatalog.FamilySuiteRoom301Id, SeededCatalog.FamilySuiteRoom302Id })
        {
            await seeder.AddConfirmedAsync(roomId, Build.Sep(10), Build.Sep(20), guests: 4);
        }

        var rooms = await NewService().SearchAsync(Query(Build.Sep(12), Build.Sep(15), 4));

        Assert.Empty(rooms);
    }

    // ---- input validation ---------------------------------------------------

    [Fact]
    public async Task Search_Throws_WhenCheckInIsMissing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(new RoomSearchQuery(null, Build.Sep(13), 2)));

        Assert.True(ex.Errors.ContainsKey("checkIn"));
    }

    [Fact]
    public async Task Search_Throws_WhenCheckOutIsMissing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(new RoomSearchQuery(Build.Sep(10), null, 2)));

        Assert.True(ex.Errors.ContainsKey("checkOut"));
    }

    [Fact]
    public async Task Search_Throws_WhenGuestCountIsMissing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(new RoomSearchQuery(Build.Sep(10), Build.Sep(13), null)));

        Assert.True(ex.Errors.ContainsKey("guests"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Search_Throws_WhenGuestCountIsBelowOne(int guests)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(Query(Build.Sep(10), Build.Sep(13), guests)));

        Assert.True(ex.Errors.ContainsKey("guests"));
    }

    [Fact]
    public async Task Search_Throws_WhenCheckOutEqualsCheckIn()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(Query(Build.Sep(13), Build.Sep(13), 2)));

        Assert.True(ex.Errors.ContainsKey("checkOut"));
    }

    [Fact]
    public async Task Search_Throws_WhenCheckOutIsBeforeCheckIn()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(Query(Build.Sep(15), Build.Sep(13), 2)));

        Assert.True(ex.Errors.ContainsKey("checkOut"));
    }

    [Fact]
    public async Task Search_ReportsEveryInvalidField_InOneException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().SearchAsync(new RoomSearchQuery(null, null, null)));

        Assert.True(ex.Errors.ContainsKey("checkIn"));
        Assert.True(ex.Errors.ContainsKey("checkOut"));
        Assert.True(ex.Errors.ContainsKey("guests"));
    }
}
