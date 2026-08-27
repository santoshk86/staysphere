namespace StaySphere.Domain;

/// <summary>Join entity linking a <see cref="RoomType"/> to an <see cref="Amenity"/>.</summary>
public class RoomTypeAmenity
{
    private RoomTypeAmenity()
    {
    }

    public RoomTypeAmenity(RoomType roomType, Amenity amenity)
    {
        RoomType = roomType ?? throw new ArgumentNullException(nameof(roomType));
        Amenity = amenity ?? throw new ArgumentNullException(nameof(amenity));
    }

    public int RoomTypeId { get; private set; }

    public RoomType RoomType { get; private set; } = null!;

    public int AmenityId { get; private set; }

    public Amenity Amenity { get; private set; } = null!;
}
