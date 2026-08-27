using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Reservations;
using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Infrastructure.Booking;
using StaySphere.Infrastructure.Persistence;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Concurrency;

/// <summary>
/// Exercises the double-booking guard (<c>BEGIN IMMEDIATE</c> + authoritative
/// re-check in <see cref="ReservationService.CreateAsync"/>) under real
/// contention: several threads, each with its own DbContext / SQLite connection,
/// race to book the same room for overlapping dates.
///
/// This is a strong smoke test of the strategy on SQLite. It is timing-dependent
/// by nature and does NOT constitute a formal proof of concurrency safety — see
/// docs/testing.md for the SQLite limitations that bound what this can show.
///
/// A file-backed database is used (not <c>:memory:</c>) because each connection
/// needs its own view of a shared database file for the write lock to mean
/// anything.
/// </summary>
public sealed class DoubleBookingConcurrencyTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"staysphere-concurrency-{Guid.NewGuid():N}.db");
    private readonly string _connectionString;

    public DoubleBookingConcurrencyTests()
    {
        _connectionString = $"Data Source={_dbPath}";

        using var context = CreateContext();
        context.Database.Migrate();
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private StaySphereDbContext CreateContext()
        => new(new DbContextOptionsBuilder<StaySphereDbContext>()
            .UseSqlite(_connectionString)
            .Options);

    private async Task<Exception?> TryBookAsync(int roomId, DateOnly checkIn, DateOnly checkOut, int index)
    {
        await using var db = CreateContext();
        var service = new ReservationService(
            db, new FixedClock(Today), new BookingReferenceGenerator(),
            NullLogger<ReservationService>.Instance);

        try
        {
            await service.CreateAsync(new CreateReservationCommand(
                roomId, checkIn, checkOut, 2, $"Racer {index}", $"racer{index}@example.com", null));
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public async Task CreateReservation_ConfirmsExactlyOne_WhenManyRequestsRaceForTheSameRoomAndDates()
    {
        const int racers = 6;
        const int roomId = SeededCatalog.FamilySuiteRoom301Id;
        var checkIn = new DateOnly(2026, 9, 10);
        var checkOut = new DateOnly(2026, 9, 13);

        using var startLine = new Barrier(racers);

        var tasks = Enumerable.Range(0, racers).Select(i => Task.Run(async () =>
        {
            startLine.SignalAndWait();
            return await TryBookAsync(roomId, checkIn, checkOut, i);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        var successes = results.Count(r => r is null);
        var failures = results.Where(r => r is not null).Select(r => r!).ToList();

        Assert.Equal(1, successes);
        Assert.Equal(racers - 1, failures.Count);
        Assert.All(failures, ex => Assert.IsType<RoomUnavailableException>(ex));

        await using var verify = CreateContext();
        var confirmed = await verify.Reservations.CountAsync(r =>
            r.RoomId == roomId && r.Status == ReservationStatus.Confirmed);
        Assert.Equal(1, confirmed);
    }

    [Fact]
    public async Task CreateReservation_ConfirmsAll_WhenConcurrentRequestsBookTheSameRoomOnDisjointDates()
    {
        const int roomId = SeededCatalog.FamilySuiteRoom301Id;

        // Four non-overlapping week-long stays for the same physical room.
        var slots = new[]
        {
            (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 8)),
            (new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 15)),
            (new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 22)),
            (new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 29)),
        };

        using var startLine = new Barrier(slots.Length);

        var tasks = slots.Select((slot, i) => Task.Run(async () =>
        {
            startLine.SignalAndWait();
            return await TryBookAsync(roomId, slot.Item1, slot.Item2, i);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, Assert.Null);

        await using var verify = CreateContext();
        Assert.Equal(slots.Length, await verify.Reservations.CountAsync(r => r.RoomId == roomId));
    }
}
