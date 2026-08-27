using System.Reflection;
using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Domain;

/// <summary>
/// <see cref="Reservation.Create"/> is the domain guard for a booking: it must
/// reject anything that would produce an invalid reservation and snapshot the
/// price at creation time.
/// </summary>
public class ReservationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Reservation CreateValid(
        Room? room = null,
        DateRange? stay = null,
        int guestCount = 2,
        string guestName = "Jordan Blake",
        string guestEmail = "jordan.blake@example.com",
        string? specialRequests = null,
        string bookingReference = "STAY-ABCDEFGH")
        => Reservation.Create(
            room ?? Build.Room(Build.RoomType(pricePerNight: 100m, maxGuests: 2)),
            stay ?? new DateRange(Build.Sep(10), Build.Sep(13)),
            guestCount, guestName, guestEmail, specialRequests, bookingReference, CreatedAt);

    [Fact]
    public void Create_WithValidInput_ProducesConfirmedActiveReservation()
    {
        var reservation = CreateValid();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.True(reservation.IsActive);
        Assert.Equal("STAY-ABCDEFGH", reservation.BookingReference);
        Assert.Equal(CreatedAt, reservation.CreatedAtUtc);
    }

    [Fact]
    public void Create_SnapshotsTotalPrice_AsPricePerNightTimesNights()
    {
        var room = Build.Room(Build.RoomType(pricePerNight: 149.50m, maxGuests: 2));

        var reservation = CreateValid(room: room, stay: new DateRange(Build.Sep(10), Build.Sep(14)));

        Assert.Equal(149.50m * 4, reservation.TotalPrice);
    }

    [Fact]
    public void Create_TrimsGuestNameAndEmail()
    {
        var reservation = CreateValid(guestName: "  Jordan Blake  ", guestEmail: "  jordan@example.com  ");

        Assert.Equal("Jordan Blake", reservation.GuestName);
        Assert.Equal("jordan@example.com", reservation.GuestEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NormalisesBlankSpecialRequestsToNull(string? requests)
    {
        var reservation = CreateValid(specialRequests: requests);

        Assert.Null(reservation.SpecialRequests);
    }

    [Fact]
    public void Create_TrimsSpecialRequests_WhenProvided()
    {
        var reservation = CreateValid(specialRequests: "  Late check-in  ");

        Assert.Equal("Late check-in", reservation.SpecialRequests);
    }

    [Fact]
    public void Create_Throws_WhenGuestCountIsZeroOrNegative()
    {
        Assert.Throws<BusinessRuleViolationException>(() => CreateValid(guestCount: 0));
        Assert.Throws<BusinessRuleViolationException>(() => CreateValid(guestCount: -1));
    }

    [Fact]
    public void Create_Throws_WhenGuestCountExceedsRoomCapacity()
    {
        var room = Build.Room(Build.RoomType(maxGuests: 2));

        var ex = Assert.Throws<BusinessRuleViolationException>(
            () => CreateValid(room: room, guestCount: 3));

        Assert.Contains("capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_Allows_GuestCountEqualToCapacity()
    {
        var room = Build.Room(Build.RoomType(maxGuests: 3));

        var reservation = CreateValid(room: room, guestCount: 3);

        Assert.Equal(3, reservation.GuestCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenGuestNameIsBlank(string? name)
    {
        Assert.Throws<BusinessRuleViolationException>(() => CreateValid(guestName: name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenGuestEmailIsBlank(string? email)
    {
        Assert.Throws<BusinessRuleViolationException>(() => CreateValid(guestEmail: email!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenBookingReferenceIsBlank(string? reference)
    {
        Assert.Throws<BusinessRuleViolationException>(() => CreateValid(bookingReference: reference!));
    }

    [Fact]
    public void Create_Throws_WhenRoomIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Reservation.Create(
            null!, new DateRange(Build.Sep(10), Build.Sep(13)), 2,
            "Jordan Blake", "jordan@example.com", null, "STAY-ABCDEFGH", CreatedAt));
    }

    [Fact]
    public void Create_Throws_WhenStayIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Reservation.Create(
            Build.Room(), null!, 2,
            "Jordan Blake", "jordan@example.com", null, "STAY-ABCDEFGH", CreatedAt));
    }

    [Fact]
    public void Create_Throws_WhenRoomTypeNavigationIsNotLoaded()
    {
        // Simulate a Room entity materialised without its RoomType navigation.
        var roomWithoutType = (Room)Activator.CreateInstance(typeof(Room), nonPublic: true)!;

        var ex = Assert.Throws<BusinessRuleViolationException>(() => Reservation.Create(
            roomWithoutType, new DateRange(Build.Sep(10), Build.Sep(13)), 2,
            "Jordan Blake", "jordan@example.com", null, "STAY-ABCDEFGH", CreatedAt));

        Assert.Contains("type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancel_MovesReservationOutOfActiveState()
    {
        var reservation = CreateValid();

        reservation.Cancel();

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.False(reservation.IsActive);
    }

    [Fact]
    public void Cancel_IsIdempotent()
    {
        var reservation = CreateValid();

        reservation.Cancel();
        reservation.Cancel();

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }
}
