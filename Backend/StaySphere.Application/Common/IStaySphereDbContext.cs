using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StaySphere.Domain;

namespace StaySphere.Application.Common;

/// <summary>
/// Persistence surface the application layer depends on. Implemented by the EF Core
/// <c>DbContext</c> in the infrastructure layer. Kept as an interface so application
/// services stay free of a concrete infrastructure reference and remain testable.
/// </summary>
public interface IStaySphereDbContext
{
    DbSet<RoomType> RoomTypes { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Amenity> Amenities { get; }

    DbSet<RoomTypeAmenity> RoomTypeAmenities { get; }

    DbSet<Reservation> Reservations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction that immediately takes a write lock (SQLite
    /// <c>BEGIN IMMEDIATE</c>) so concurrent booking operations serialize around
    /// the check-then-insert critical section. See <c>docs/decisions.md</c>.
    /// </summary>
    Task<IDbContextTransaction> BeginImmediateTransactionAsync(CancellationToken cancellationToken = default);
}
