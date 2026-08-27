using StaySphere.Domain.Common;

namespace StaySphere.Domain;

/// <summary>
/// A guest booking of a concrete <see cref="Room"/> for a <see cref="DateRange"/>.
/// Created through <see cref="Create"/>, which enforces the booking invariants;
/// there are no public setters so an invalid reservation cannot be constructed.
/// </summary>
public class Reservation
{
    private Reservation()
    {
    }

    public int Id { get; private set; }

    public string BookingReference { get; private set; } = null!;

    public int RoomId { get; private set; }

    public Room Room { get; private set; } = null!;

    public DateRange Stay { get; private set; } = null!;

    public int GuestCount { get; private set; }

    public string GuestName { get; private set; } = null!;

    public string GuestEmail { get; private set; } = null!;

    public string? SpecialRequests { get; private set; }

    public ReservationStatus Status { get; private set; }

    /// <summary>Price snapshot taken at booking time (nights * price per night).</summary>
    public decimal TotalPrice { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsActive => Status == ReservationStatus.Confirmed;

    public static Reservation Create(
        Room room,
        DateRange stay,
        int guestCount,
        string guestName,
        string guestEmail,
        string? specialRequests,
        string bookingReference,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(stay);

        if (room.RoomType is null)
        {
            throw new BusinessRuleViolationException("The room's type must be loaded to create a reservation.");
        }

        if (guestCount < 1)
        {
            throw new BusinessRuleViolationException("A reservation must have at least one guest.");
        }

        if (guestCount > room.RoomType.MaxGuests)
        {
            throw new BusinessRuleViolationException(
                $"Guest count {guestCount} exceeds the room capacity of {room.RoomType.MaxGuests}.");
        }

        if (string.IsNullOrWhiteSpace(guestName))
        {
            throw new BusinessRuleViolationException("Guest name is required.");
        }

        if (string.IsNullOrWhiteSpace(guestEmail))
        {
            throw new BusinessRuleViolationException("Guest email is required.");
        }

        if (string.IsNullOrWhiteSpace(bookingReference))
        {
            throw new BusinessRuleViolationException("Booking reference is required.");
        }

        return new Reservation
        {
            Room = room,
            RoomId = room.Id,
            Stay = stay,
            GuestCount = guestCount,
            GuestName = guestName.Trim(),
            GuestEmail = guestEmail.Trim(),
            SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim(),
            BookingReference = bookingReference.Trim(),
            Status = ReservationStatus.Confirmed,
            TotalPrice = room.RoomType.CalculatePrice(stay.Nights),
            CreatedAtUtc = createdAtUtc
        };
    }

    public void Cancel()
    {
        Status = ReservationStatus.Cancelled;
    }
}
