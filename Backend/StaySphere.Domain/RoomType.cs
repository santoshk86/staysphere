using StaySphere.Domain.Common;

namespace StaySphere.Domain;

/// <summary>
/// A category of room (e.g. "Deluxe King"). Price, capacity, description and
/// amenities are shared by every physical <see cref="Room"/> of this type, so
/// they belong here rather than on the individual room.
/// </summary>
public class RoomType
{
    private readonly List<Room> _rooms = new();
    private readonly List<RoomTypeAmenity> _roomTypeAmenities = new();

    private RoomType()
    {
    }

    public RoomType(string name, string description, decimal pricePerNight, int maxGuests, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Room type name is required.");
        }

        if (pricePerNight <= 0)
        {
            throw new BusinessRuleViolationException("Price per night must be greater than zero.");
        }

        if (maxGuests <= 0)
        {
            throw new BusinessRuleViolationException("Maximum guests must be greater than zero.");
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        PricePerNight = pricePerNight;
        MaxGuests = maxGuests;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? "/images/room-placeholder.svg" : imageUrl.Trim();
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public decimal PricePerNight { get; private set; }

    public int MaxGuests { get; private set; }

    /// <summary>Placeholder image path; a real asset/CDN URL can replace it later.</summary>
    public string ImageUrl { get; private set; } = null!;

    public IReadOnlyCollection<Room> Rooms => _rooms;

    public IReadOnlyCollection<RoomTypeAmenity> RoomTypeAmenities => _roomTypeAmenities;

    /// <summary>Total price for a stay of the given number of nights.</summary>
    public decimal CalculatePrice(int nights)
    {
        if (nights <= 0)
        {
            throw new BusinessRuleViolationException("A stay must be at least one night.");
        }

        return PricePerNight * nights;
    }
}
