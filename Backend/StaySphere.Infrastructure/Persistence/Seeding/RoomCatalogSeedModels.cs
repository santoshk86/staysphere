namespace StaySphere.Infrastructure.Persistence.Seeding;

/// <summary>Shape of a room seed JSON file (see <c>StaySphere.Api/Data/room-seed.json</c>).</summary>
internal sealed class RoomCatalogSeedFile
{
    public List<AmenitySeed>? Amenities { get; set; }

    public List<RoomTypeSeed>? RoomTypes { get; set; }

    public List<RoomSeed>? Rooms { get; set; }
}

internal sealed class AmenitySeed
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class RoomTypeSeed
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal PricePerNight { get; set; }

    public int MaxGuests { get; set; }

    public string? ImageUrl { get; set; }

    public List<int>? AmenityIds { get; set; }
}

internal sealed class RoomSeed
{
    public int Id { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int RoomTypeId { get; set; }
}
