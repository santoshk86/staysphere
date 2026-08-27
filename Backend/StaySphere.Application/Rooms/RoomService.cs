using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StaySphere.Application.Common;
using StaySphere.Domain;
using StaySphere.Domain.Common;

namespace StaySphere.Application.Rooms;

public sealed class RoomService : IRoomService
{
    private readonly IStaySphereDbContext _db;
    private readonly ILogger<RoomService> _logger;

    public RoomService(IStaySphereDbContext db, ILogger<RoomService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoomDto>> SearchAsync(RoomSearchQuery query, CancellationToken cancellationToken = default)
    {
        var stay = BuildValidatedRange(query);
        var guests = query.Guests!.Value;

        try
        {
            // Availability is resolved entirely in SQL: a room is excluded when it has
            // any confirmed reservation whose interval overlaps the requested
            // [CheckIn, CheckOut). Nothing is filtered in memory.
            var rooms = await _db.Rooms
                .AsNoTracking()
                .Include(room => room.RoomType)
                    .ThenInclude(type => type.RoomTypeAmenities)
                    .ThenInclude(link => link.Amenity)
                .Where(room => room.RoomType.MaxGuests >= guests)
                .Where(room => !room.Reservations.Any(reservation =>
                    reservation.Status == ReservationStatus.Confirmed &&
                    reservation.Stay.Start < stay.End &&
                    stay.Start < reservation.Stay.End))
                .OrderBy(room => room.RoomType.PricePerNight)
                .ThenBy(room => room.RoomNumber)
                .ToListAsync(cancellationToken);

            return rooms.Select(RoomDto.FromEntity).ToList();
        }
        catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DomainException)
        {
            _logger.LogError(ex, "Room search failed for {CheckIn}..{CheckOut}, {Guests} guest(s)",
                stay.Start, stay.End, guests);
            throw;
        }
    }

    public async Task<RoomDto> GetByIdAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .Include(r => r.RoomType)
                .ThenInclude(type => type.RoomTypeAmenities)
                .ThenInclude(link => link.Amenity)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);

        if (room is null)
        {
            throw new NotFoundException($"Room {roomId} was not found.");
        }

        return RoomDto.FromEntity(room);
    }

    private static DateRange BuildValidatedRange(RoomSearchQuery query)
    {
        var errors = new ValidationErrors();

        if (query.CheckIn is null)
        {
            errors.Add("checkIn", "Check-in date is required.");
        }

        if (query.CheckOut is null)
        {
            errors.Add("checkOut", "Check-out date is required.");
        }

        if (query.Guests is null)
        {
            errors.Add("guests", "Guest count is required.");
        }
        else if (query.Guests < 1)
        {
            errors.Add("guests", "Guest count must be at least 1.");
        }

        if (query.CheckIn is { } checkIn && query.CheckOut is { } checkOut && checkOut <= checkIn)
        {
            errors.Add("checkOut", "Check-out date must be after the check-in date.");
        }

        errors.ThrowIfAny();

        return new DateRange(query.CheckIn!.Value, query.CheckOut!.Value);
    }
}
