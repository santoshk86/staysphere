using StaySphere.Domain;

namespace StaySphere.Application.Rooms;

/// <summary>Inputs for a room search. Nullable so missing values can be reported as validation errors.</summary>
public sealed record RoomSearchQuery(DateOnly? CheckIn, DateOnly? CheckOut, int? Guests);

/// <summary>
/// A room as returned by search and details. Search returns one entry per available
/// physical room; a client may group by <see cref="RoomTypeId"/> if it prefers a
/// per-category listing.
/// </summary>
public sealed record RoomDto(
    int RoomId,
    string RoomNumber,
    int RoomTypeId,
    string RoomType,
    string Description,
    decimal PricePerNight,
    int MaxGuests,
    IReadOnlyList<string> Amenities,
    string ImageUrl)
{
    internal static RoomDto FromEntity(Room room) => new(
        room.Id,
        room.RoomNumber,
        room.RoomTypeId,
        room.RoomType.Name,
        room.RoomType.Description,
        room.RoomType.PricePerNight,
        room.RoomType.MaxGuests,
        room.RoomType.RoomTypeAmenities
            .Select(link => link.Amenity.Name)
            .OrderBy(name => name)
            .ToList(),
        room.RoomType.ImageUrl);
}
