using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Common;
using StaySphere.Application.Reservations;
using StaySphere.Application.Rooms;
using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Application;

/// <summary>
/// Behaviour of the "create reservation" use case: validation, capacity, the
/// authoritative availability re-check, reference generation, persistence and the
/// confirmation payload. Runs against a real SQLite database with the shipped
/// catalog and a fixed clock (today = 2026-09-01).
/// </summary>
public sealed class ReservationServiceCreateTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedClock _clock = new(Today);
    private readonly FakeBookingReferenceGenerator _references = new();

    public void Dispose() => _database.Dispose();

    private ReservationService NewService(FakeBookingReferenceGenerator? references = null)
        => new(_database.CreateContext(), _clock, references ?? _references, NullLogger<ReservationService>.Instance);

    private static CreateReservationCommand Command(
        int roomId = SeededCatalog.StandardQueenRoom101Id,
        DateOnly? checkIn = null,
        DateOnly? checkOut = null,
        int guestCount = 2,
        string? guestName = "Jordan Blake",
        string? guestEmail = "jordan.blake@example.com",
        string? specialRequests = null)
        => new(roomId, checkIn ?? new DateOnly(2026, 9, 10), checkOut ?? new DateOnly(2026, 9, 13),
            guestCount, guestName, guestEmail, specialRequests);

    // ---- success path -------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsConfirmation_WithReferencePriceAndStay()
    {
        var confirmation = await NewService().CreateAsync(
            Command(specialRequests: "  Late check-in  "));

        Assert.Equal("STAY-TEST0001", confirmation.BookingReference);
        Assert.Equal("Confirmed", confirmation.Status);
        Assert.Equal(new DateOnly(2026, 9, 10), confirmation.CheckIn);
        Assert.Equal(new DateOnly(2026, 9, 13), confirmation.CheckOut);
        Assert.Equal(3, confirmation.Nights);
        Assert.Equal(SeededCatalog.StandardQueenPrice, confirmation.PricePerNight);
        Assert.Equal(SeededCatalog.StandardQueenPrice * 3, confirmation.TotalPrice);
        Assert.Equal("Late check-in", confirmation.SpecialRequests);
        Assert.Equal("101", confirmation.RoomNumber);
        Assert.NotEmpty(confirmation.Amenities);
    }

    [Fact]
    public async Task Create_PersistsAConfirmedReservation_ReadableFromAnotherContext()
    {
        var confirmation = await NewService().CreateAsync(Command());

        await using var verify = _database.CreateContext();
        var stored = await verify.Reservations
            .Include(r => r.Room)
            .SingleAsync(r => r.BookingReference == confirmation.BookingReference);

        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Equal(SeededCatalog.StandardQueenRoom101Id, stored.RoomId);
        Assert.Equal(new DateOnly(2026, 9, 10), stored.Stay.Start);
        Assert.Equal(new DateOnly(2026, 9, 13), stored.Stay.End);
        Assert.Equal(SeededCatalog.StandardQueenPrice * 3, stored.TotalPrice);
    }

    [Fact]
    public async Task Create_GeneratesAUniqueReference_RetryingWhenTheFirstCandidateCollides()
    {
        // Pre-seed a reservation on a different room that already owns "STAY-DUP0001".
        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            SeededCatalog.DeluxeKingRoom201Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3),
            reference: "STAY-DUP0001");

        var references = new FakeBookingReferenceGenerator("STAY-DUP0001", "STAY-DUP0001", "STAY-UNIQUE99");

        var confirmation = await NewService(references).CreateAsync(Command());

        Assert.Equal("STAY-UNIQUE99", confirmation.BookingReference);
        Assert.Equal(3, references.GenerateCallCount);
    }

    [Fact]
    public async Task Create_Throws_WhenAUniqueReferenceCannotBeGenerated()
    {
        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            SeededCatalog.DeluxeKingRoom201Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3),
            reference: "STAY-ALWAYSDUP");

        // The generator keeps handing back a reference that is already taken.
        var references = new FakeBookingReferenceGenerator(
            Enumerable.Repeat("STAY-ALWAYSDUP", 10).ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(references).CreateAsync(Command()));

        await using var verify = _database.CreateContext();
        Assert.Equal(1, await verify.Reservations.CountAsync()); // only the seeded one
    }

    // ---- room existence ---------------------------------------------------

    [Fact]
    public async Task Create_Throws_NotFound_WhenRoomDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => NewService().CreateAsync(Command(roomId: 9999)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Create_Throws_Validation_WhenRoomIdIsNotPositive(int roomId)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(roomId: roomId)));

        Assert.True(ex.Errors.ContainsKey("roomId"));
    }

    // ---- date / guest / contact validation ------------------------------

    [Fact]
    public async Task Create_Throws_Validation_WhenCheckInIsMissing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command() with { CheckIn = null }));

        Assert.True(ex.Errors.ContainsKey("checkIn"));
    }

    [Fact]
    public async Task Create_Throws_Validation_WhenCheckOutIsMissing()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command() with { CheckOut = null }));

        Assert.True(ex.Errors.ContainsKey("checkOut"));
    }

    [Fact]
    public async Task Create_Throws_Validation_WhenCheckOutIsNotAfterCheckIn()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(
                checkIn: new DateOnly(2026, 9, 13), checkOut: new DateOnly(2026, 9, 13))));

        Assert.True(ex.Errors.ContainsKey("checkOut"));
    }

    [Fact]
    public async Task Create_Throws_Validation_WhenCheckInIsInThePast()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(
                checkIn: Today.AddDays(-1), checkOut: Today.AddDays(2))));

        Assert.True(ex.Errors.ContainsKey("checkIn"));
    }

    [Fact]
    public async Task Create_Allows_CheckInEqualToToday()
    {
        var confirmation = await NewService().CreateAsync(Command(
            checkIn: Today, checkOut: Today.AddDays(2)));

        Assert.Equal(Today, confirmation.CheckIn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Create_Throws_Validation_WhenGuestCountIsBelowOne(int guests)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(guestCount: guests)));

        Assert.True(ex.Errors.ContainsKey("guestCount"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public async Task Create_Throws_Validation_WhenGuestNameIsMissingOrTooShort(string? name)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(guestName: name)));

        Assert.True(ex.Errors.ContainsKey("guestName"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("spaces in@example.com")]
    public async Task Create_Throws_Validation_WhenGuestEmailIsMissingOrInvalid(string? email)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(guestEmail: email)));

        Assert.True(ex.Errors.ContainsKey("guestEmail"));
    }

    [Fact]
    public async Task Create_Throws_Validation_WhenSpecialRequestsExceedTheLimit()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(specialRequests: new string('x', 1001))));

        Assert.True(ex.Errors.ContainsKey("specialRequests"));
    }

    [Fact]
    public async Task Create_Allows_SpecialRequestsExactlyAtTheLimit()
    {
        var confirmation = await NewService().CreateAsync(
            Command(specialRequests: new string('x', 1000)));

        Assert.Equal(1000, confirmation.SpecialRequests!.Length);
    }

    [Fact]
    public async Task Create_DoesNotPersistAnything_WhenValidationFails()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(guestEmail: "bad")));

        await using var verify = _database.CreateContext();
        Assert.Equal(0, await verify.Reservations.CountAsync());
    }

    // ---- capacity --------------------------------------------------------

    [Fact]
    public async Task Create_Throws_Validation_WhenGuestCountExceedsRoomCapacity()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Command(
                roomId: SeededCatalog.StandardQueenRoom101Id, guestCount: 3)));

        Assert.True(ex.Errors.ContainsKey("guestCount"));
    }

    [Fact]
    public async Task Create_Allows_GuestCountEqualToRoomCapacity()
    {
        var confirmation = await NewService().CreateAsync(Command(
            roomId: SeededCatalog.FamilySuiteRoom301Id, guestCount: SeededCatalog.FamilySuiteCapacity));

        Assert.Equal(SeededCatalog.FamilySuiteCapacity, confirmation.GuestCount);
    }

    // ---- authoritative availability re-check --------------------------

    [Fact]
    public async Task Create_Throws_Conflict_WhenAnOverlappingConfirmedReservationAlreadyExists()
    {
        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            SeededCatalog.StandardQueenRoom101Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13));

        await Assert.ThrowsAsync<RoomUnavailableException>(
            () => NewService().CreateAsync(Command(
                checkIn: new DateOnly(2026, 9, 12), checkOut: new DateOnly(2026, 9, 15))));
    }

    [Fact]
    public async Task Create_Succeeds_WhenTheExistingReservationIsMerelyAdjacent()
    {
        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            SeededCatalog.StandardQueenRoom101Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13));

        var confirmation = await NewService().CreateAsync(Command(
            checkIn: new DateOnly(2026, 9, 13), checkOut: new DateOnly(2026, 9, 15)));

        Assert.Equal("Confirmed", confirmation.Status);
    }

    [Fact]
    public async Task Create_RevalidatesAvailability_EvenIfAPriorSearchSaidTheRoomWasFree()
    {
        // "Stale search result": at search time room 101 is free for these dates.
        var search = new StaySphere.Application.Rooms.RoomService(
            _database.CreateContext(), NullLogger<StaySphere.Application.Rooms.RoomService>.Instance);
        var beforeBooking = await search.SearchAsync(
            new StaySphere.Application.Rooms.RoomSearchQuery(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), 2));
        Assert.Contains(beforeBooking, r => r.RoomNumber == "101");

        // Someone else books it.
        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            SeededCatalog.StandardQueenRoom101Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13));

        // Our create for the same slot must not trust the earlier search.
        await Assert.ThrowsAsync<RoomUnavailableException>(
            () => NewService().CreateAsync(Command(
                checkIn: new DateOnly(2026, 9, 10), checkOut: new DateOnly(2026, 9, 13))));
    }

    [Fact]
    public async Task Create_Succeeds_WhenOnlyOverlappingReservationIsCancelled()
    {
        await new ReservationSeeder(_database.CreateContext()).AddCancelledAsync(
            SeededCatalog.StandardQueenRoom101Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13));

        var confirmation = await NewService().CreateAsync(Command(
            checkIn: new DateOnly(2026, 9, 10), checkOut: new DateOnly(2026, 9, 13)));

        Assert.Equal("Confirmed", confirmation.Status);
    }

    [Fact]
    public async Task Create_LeavesExactlyOneReservation_WhenASecondOverlappingAttemptIsRejected()
    {
        await NewService().CreateAsync(Command(
            checkIn: new DateOnly(2026, 9, 10), checkOut: new DateOnly(2026, 9, 13)));

        await Assert.ThrowsAsync<RoomUnavailableException>(
            () => NewService().CreateAsync(Command(
                checkIn: new DateOnly(2026, 9, 11), checkOut: new DateOnly(2026, 9, 14))));

        await using var verify = _database.CreateContext();
        Assert.Equal(1, await verify.Reservations.CountAsync());
    }
}
