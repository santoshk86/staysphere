using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StaySphere.Application.Common;
using StaySphere.Domain;
using StaySphere.Domain.Common;

namespace StaySphere.Application.Reservations;

public sealed class ReservationService : IReservationService
{
    private const int MaxReferenceAttempts = 5;
    private const int MaxSpecialRequestsLength = 1000;

    private readonly IStaySphereDbContext _db;
    private readonly IClock _clock;
    private readonly IBookingReferenceGenerator _references;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(
        IStaySphereDbContext db,
        IClock clock,
        IBookingReferenceGenerator references,
        ILogger<ReservationService> logger)
    {
        _db = db;
        _clock = clock;
        _references = references;
        _logger = logger;
    }

    public async Task<ReservationConfirmation> CreateAsync(CreateReservationCommand command, CancellationToken cancellationToken = default)
    {
        var stay = ValidateRequest(command);

        _logger.LogInformation(
            "Reservation attempt: room {RoomId}, {CheckIn}..{CheckOut}, {Guests} guest(s)",
            command.RoomId, stay.Start, stay.End, command.GuestCount);

        var room = await _db.Rooms
            .Include(r => r.RoomType)
                .ThenInclude(type => type.RoomTypeAmenities)
                .ThenInclude(link => link.Amenity)
            .FirstOrDefaultAsync(r => r.Id == command.RoomId, cancellationToken);

        if (room is null)
        {
            throw new NotFoundException($"Room {command.RoomId} was not found.");
        }

        if (command.GuestCount > room.RoomType.MaxGuests)
        {
            throw new ValidationException("guestCount",
                $"This room accommodates up to {room.RoomType.MaxGuests} guest(s).");
        }

        // Authoritative availability re-check. The search result is never trusted here.
        // BEGIN IMMEDIATE serializes concurrent bookings so the check-then-insert below
        // cannot interleave with another booking for the same room.
        await using var transaction = await _db.BeginImmediateTransactionAsync(cancellationToken);

        var hasConflict = await _db.Reservations.AnyAsync(reservation =>
            reservation.RoomId == room.Id &&
            reservation.Status == ReservationStatus.Confirmed &&
            reservation.Stay.Start < stay.End &&
            stay.Start < reservation.Stay.End, cancellationToken);

        if (hasConflict)
        {
            _logger.LogWarning(
                "Booking conflict: room {RoomId} is already reserved for {CheckIn}..{CheckOut}",
                room.Id, stay.Start, stay.End);
            throw new RoomUnavailableException(
                "The selected room is no longer available for the requested dates.");
        }

        var reference = await GenerateUniqueReferenceAsync(cancellationToken);

        var reservation = Reservation.Create(
            room,
            stay,
            command.GuestCount,
            command.GuestName!,
            command.GuestEmail!,
            command.SpecialRequests,
            reference,
            _clock.UtcNow);

        _db.Reservations.Add(reservation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Persistence conflict while creating a reservation for room {RoomId}", room.Id);
            throw new RoomUnavailableException(
                "The selected room is no longer available for the requested dates.");
        }

        _logger.LogInformation(
            "Reservation {Reference} confirmed for room {RoomId} ({CheckIn}..{CheckOut}), total {Total}",
            reference, room.Id, stay.Start, stay.End, reservation.TotalPrice);

        return ReservationConfirmation.FromEntity(reservation);
    }

    public async Task<ReservationConfirmation> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ValidationException("reference", "Booking reference is required.");
        }

        var reservation = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Room)
                .ThenInclude(room => room.RoomType)
                .ThenInclude(type => type.RoomTypeAmenities)
                .ThenInclude(link => link.Amenity)
            .FirstOrDefaultAsync(r => r.BookingReference == reference, cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException($"Reservation '{reference}' was not found.");
        }

        return ReservationConfirmation.FromEntity(reservation);
    }

    private DateRange ValidateRequest(CreateReservationCommand command)
    {
        var errors = new ValidationErrors();

        if (command.RoomId <= 0)
        {
            errors.Add("roomId", "A valid room must be selected.");
        }

        if (command.CheckIn is null)
        {
            errors.Add("checkIn", "Check-in date is required.");
        }

        if (command.CheckOut is null)
        {
            errors.Add("checkOut", "Check-out date is required.");
        }

        if (command.CheckIn is { } checkIn)
        {
            if (command.CheckOut is { } checkOut && checkOut <= checkIn)
            {
                errors.Add("checkOut", "Check-out date must be after the check-in date.");
            }

            if (checkIn < _clock.Today)
            {
                errors.Add("checkIn", "Check-in date cannot be in the past.");
            }
        }

        if (command.GuestCount < 1)
        {
            errors.Add("guestCount", "At least one guest is required.");
        }

        if (string.IsNullOrWhiteSpace(command.GuestName))
        {
            errors.Add("guestName", "Guest name is required.");
        }
        else if (command.GuestName.Trim().Length < 2)
        {
            errors.Add("guestName", "Guest name is too short.");
        }

        if (string.IsNullOrWhiteSpace(command.GuestEmail))
        {
            errors.Add("guestEmail", "Guest email is required.");
        }
        else if (!IsValidEmail(command.GuestEmail))
        {
            errors.Add("guestEmail", "Guest email is not a valid email address.");
        }

        if (command.SpecialRequests is { Length: > MaxSpecialRequestsLength })
        {
            errors.Add("specialRequests", $"Special requests must be {MaxSpecialRequestsLength} characters or fewer.");
        }

        errors.ThrowIfAny();

        return new DateRange(command.CheckIn!.Value, command.CheckOut!.Value);
    }

    private static bool IsValidEmail(string email)
    {
        var trimmed = email.Trim();
        return MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed!.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GenerateUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxReferenceAttempts; attempt++)
        {
            var candidate = _references.Generate();
            var exists = await _db.Reservations.AnyAsync(r => r.BookingReference == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique booking reference after several attempts.");
    }
}
