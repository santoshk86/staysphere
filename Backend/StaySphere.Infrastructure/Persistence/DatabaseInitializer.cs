using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Infrastructure.Persistence.Seeding;

namespace StaySphere.Infrastructure.Persistence;

/// <summary>
/// Applies migrations, enables WAL mode, seeds extra rooms from JSON files, and
/// seeds a small set of date-relative sample reservations (the base catalog is
/// seeded through migration data). Safe to run on every startup.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly StaySphereDbContext _db;
    private readonly JsonRoomCatalogSeeder _roomCatalogSeeder;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        StaySphereDbContext db,
        JsonRoomCatalogSeeder roomCatalogSeeder,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _roomCatalogSeeder = roomCatalogSeeder;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken);

        // WAL lets readers run while a writer holds the lock; the setting is persisted
        // in the database file so this is effectively a one-time switch.
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        // Idempotent: only inserts rooms whose explicit id is not already in the DB.
        await _roomCatalogSeeder.SeedAsync(cancellationToken);

        await SeedSampleReservationsAsync(cancellationToken);
    }

    private async Task SeedSampleReservationsAsync(CancellationToken cancellationToken)
    {
        if (await _db.Reservations.AnyAsync(cancellationToken))
        {
            return;
        }

        var rooms = await _db.Rooms
            .Include(r => r.RoomType)
            .OrderBy(r => r.RoomNumber)
            .ToListAsync(cancellationToken);

        if (rooms.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seeded = 0;

        void Book(Room room, int startOffsetDays, int nights, string name, string email, int guests, string? notes)
        {
            var stay = new DateRange(today.AddDays(startOffsetDays), today.AddDays(startOffsetDays + nights));
            var reservation = Reservation.Create(room, stay, guests, name, email, notes,
                $"STAY-SEED{room.RoomNumber}", DateTimeOffset.UtcNow);
            _db.Reservations.Add(reservation);
            seeded++;
        }

        // Occupy one room in two different types for a near-future window so that
        // searches over those dates demonstrate filtered availability, while other
        // rooms of the same types remain bookable.
        var firstRoom = rooms[0];
        Book(firstRoom, 3, 3, "Ava Thompson", "ava.thompson@example.com", 2, "High floor if possible.");

        var otherTypeRoom = rooms.FirstOrDefault(r => r.RoomTypeId != firstRoom.RoomTypeId);
        if (otherTypeRoom is not null)
        {
            Book(otherTypeRoom, 10, 2, "Liam Carter", "liam.carter@example.com", 1, null);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} sample reservation(s).", seeded);
    }
}
