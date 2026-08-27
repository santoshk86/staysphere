using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StaySphere.Application.Common;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence;

public sealed class StaySphereDbContext : DbContext, IStaySphereDbContext
{
    public StaySphereDbContext(DbContextOptions<StaySphereDbContext> options) : base(options)
    {
    }

    public DbSet<RoomType> RoomTypes => Set<RoomType>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Amenity> Amenities => Set<Amenity>();

    public DbSet<RoomTypeAmenity> RoomTypeAmenities => Set<RoomTypeAmenity>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public async Task<IDbContextTransaction> BeginImmediateTransactionAsync(CancellationToken cancellationToken = default)
    {
        // EF Core's BeginTransaction issues a deferred "BEGIN", which only takes a write
        // lock on the first write. For the booking critical section we need the write
        // lock up front so a concurrent booking for the same room cannot slip between
        // our availability check and our insert. "BEGIN IMMEDIATE" (deferred: false)
        // takes the RESERVED lock immediately; other writers wait (busy timeout) until
        // this transaction commits.
        var connection = (SqliteConnection)Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var sqliteTransaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        return await Database.UseTransactionAsync(sqliteTransaction, cancellationToken)
            ?? throw new InvalidOperationException("Failed to start a database transaction.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StaySphereDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
