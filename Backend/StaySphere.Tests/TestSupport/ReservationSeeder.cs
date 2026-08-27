using Microsoft.EntityFrameworkCore;
using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Infrastructure.Persistence;

namespace StaySphere.Tests.TestSupport;

/// <summary>
/// Inserts reservations directly through the domain factory (not the application
/// service) so a test can set up "an existing booking" without going through the
/// code under test. Booking references are unique per process.
/// </summary>
public sealed class ReservationSeeder
{
    private static int _sequence;

    private readonly StaySphereDbContext _db;

    public ReservationSeeder(StaySphereDbContext db) => _db = db;

    public Task<Reservation> AddConfirmedAsync(
        int roomId, DateOnly checkIn, DateOnly checkOut, int guests = 1, string? reference = null)
        => AddAsync(roomId, checkIn, checkOut, guests, cancelled: false, reference);

    public Task<Reservation> AddCancelledAsync(
        int roomId, DateOnly checkIn, DateOnly checkOut, int guests = 1)
        => AddAsync(roomId, checkIn, checkOut, guests, cancelled: true, reference: null);

    private async Task<Reservation> AddAsync(
        int roomId, DateOnly checkIn, DateOnly checkOut, int guests, bool cancelled, string? reference)
    {
        var room = await _db.Rooms.Include(r => r.RoomType).SingleAsync(r => r.Id == roomId);

        var reservation = Reservation.Create(
            room,
            new DateRange(checkIn, checkOut),
            guests,
            "Existing Guest",
            "existing.guest@example.com",
            specialRequests: null,
            reference ?? $"STAY-SEED{Interlocked.Increment(ref _sequence):D5}",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        if (cancelled)
        {
            reservation.Cancel();
        }

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        return reservation;
    }
}
