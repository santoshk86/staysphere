using StaySphere.Domain.Common;

namespace StaySphere.Domain;

/// <summary>A feature offered by a room type (e.g. "Free Wi-Fi", "Air conditioning").</summary>
public class Amenity
{
    private readonly List<RoomTypeAmenity> _roomTypeAmenities = new();

    private Amenity()
    {
    }

    public Amenity(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Amenity name is required.");
        }

        Name = name.Trim();
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = null!;

    public IReadOnlyCollection<RoomTypeAmenity> RoomTypeAmenities => _roomTypeAmenities;
}
