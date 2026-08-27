using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Reservations;
using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Application;

/// <summary>
/// The core booking invariant: two overlapping confirmed reservations must never
/// both exist for the same physical room. Each case seeds one existing confirmed
/// reservation and then tries to create a second one for the same room.
/// </summary>
public sealed class ReservationServiceConflictTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 1);
    private const int Room = SeededCatalog.FamilySuiteRoom301Id;

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedClock _clock = new(Today);

    public void Dispose() => _database.Dispose();

    private ReservationService NewService()
        => new(_database.CreateContext(), _clock, new FakeBookingReferenceGenerator(),
            NullLogger<ReservationService>.Instance);

    public static TheoryData<string, int, int, int, int, bool> Cases() => new()
    {
        // description, existingStart, existingEnd, newStart, newEnd, expectConflict
        { "new starts inside existing, ends after (overlap at end)", 10, 13, 12, 15, true },
        { "new starts exactly when existing ends (adjacent)",        10, 13, 13, 15, false },
        { "existing spans a superset of the new range",              10, 20, 12, 15, true },
        { "new range is a superset of the existing range",           12, 15, 10, 20, true },
        { "new ends exactly when existing starts (adjacent)",        10, 13, 8, 10, false },
        { "exact same range",                                        10, 13, 10, 13, true },
        { "new starts before existing, ends inside (overlap start)", 10, 13, 8, 11, true },
        { "new entirely before existing, not touching",              10, 13, 5, 8, false },
        { "new entirely after existing, not touching",               10, 13, 16, 18, false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CreateReservation_HonoursHalfOpenOverlapRule(
        string description, int existingStart, int existingEnd, int newStart, int newEnd, bool expectConflict)
    {
        _ = description;

        await new ReservationSeeder(_database.CreateContext()).AddConfirmedAsync(
            Room, new DateOnly(2026, 9, existingStart), new DateOnly(2026, 9, existingEnd), guests: 2);

        var command = new CreateReservationCommand(
            Room, new DateOnly(2026, 9, newStart), new DateOnly(2026, 9, newEnd),
            2, "Second Guest", "second.guest@example.com", null);

        if (expectConflict)
        {
            await Assert.ThrowsAsync<RoomUnavailableException>(() => NewService().CreateAsync(command));

            await using var verify = _database.CreateContext();
            Assert.Equal(1, await verify.Reservations.CountAsync());
        }
        else
        {
            var confirmation = await NewService().CreateAsync(command);
            Assert.Equal("Confirmed", confirmation.Status);

            await using var verify = _database.CreateContext();
            Assert.Equal(2, await verify.Reservations.CountAsync());
        }
    }
}
