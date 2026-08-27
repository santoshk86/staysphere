namespace StaySphere.Api.Contracts;

/// <summary>Request body for <c>POST /api/reservations</c>. Validation lives in the application layer.</summary>
public sealed class CreateReservationRequest
{
    public int RoomId { get; init; }

    public DateOnly? CheckIn { get; init; }

    public DateOnly? CheckOut { get; init; }

    public int GuestCount { get; init; }

    public string? GuestName { get; init; }

    public string? GuestEmail { get; init; }

    public string? SpecialRequests { get; init; }
}
