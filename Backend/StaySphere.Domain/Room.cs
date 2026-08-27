using StaySphere.Domain.Common;

namespace StaySphere.Domain;

/// <summary>
/// A physical, bookable inventory unit (e.g. room "101"). Reservations reference
/// a concrete room so availability can be determined per unit, not per category.
/// </summary>
public class Room
{
    private readonly List<Reservation> _reservations = new();

    private Room()
    {
    }

    public Room(string roomNumber, RoomType roomType)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            throw new BusinessRuleViolationException("Room number is required.");
        }

        RoomNumber = roomNumber.Trim();
        RoomType = roomType ?? throw new ArgumentNullException(nameof(roomType));
    }

    public int Id { get; private set; }

    public string RoomNumber { get; private set; } = null!;

    public int RoomTypeId { get; private set; }

    public RoomType RoomType { get; private set; } = null!;

    public IReadOnlyCollection<Reservation> Reservations => _reservations;
}
