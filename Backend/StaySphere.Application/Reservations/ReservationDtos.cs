using StaySphere.Domain;

namespace StaySphere.Application.Reservations;

/// <summary>Everything needed to create a reservation. Nullable dates so absence is a validation error.</summary>
public sealed record CreateReservationCommand(
    int RoomId,
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    int GuestCount,
    string? GuestName,
    string? GuestEmail,
    string? SpecialRequests);

/// <summary>Confirmation payload returned after booking and by confirmation retrieval.</summary>
public sealed record ReservationConfirmation(
    string BookingReference,
    string GuestName,
    string GuestEmail,
    string? SpecialRequests,
    int RoomId,
    string RoomNumber,
    string RoomType,
    string Description,
    IReadOnlyList<string> Amenities,
    string ImageUrl,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int GuestCount,
    decimal PricePerNight,
    decimal TotalPrice,
    string Status,
    DateTimeOffset CreatedAtUtc)
{
    internal static ReservationConfirmation FromEntity(Reservation reservation)
    {
        var type = reservation.Room.RoomType;

        return new ReservationConfirmation(
            reservation.BookingReference,
            reservation.GuestName,
            reservation.GuestEmail,
            reservation.SpecialRequests,
            reservation.RoomId,
            reservation.Room.RoomNumber,
            type.Name,
            type.Description,
            type.RoomTypeAmenities.Select(link => link.Amenity.Name).OrderBy(name => name).ToList(),
            type.ImageUrl,
            reservation.Stay.Start,
            reservation.Stay.End,
            reservation.Stay.Nights,
            reservation.GuestCount,
            type.PricePerNight,
            reservation.TotalPrice,
            reservation.Status.ToString(),
            reservation.CreatedAtUtc);
    }
}
