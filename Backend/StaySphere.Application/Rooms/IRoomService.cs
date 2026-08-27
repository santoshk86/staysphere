namespace StaySphere.Application.Rooms;

public interface IRoomService
{
    /// <summary>Rooms available for the whole requested range with capacity for the guest count.</summary>
    Task<IReadOnlyList<RoomDto>> SearchAsync(RoomSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Full details for a single physical room. Throws <c>NotFoundException</c> when it does not exist.</summary>
    Task<RoomDto> GetByIdAsync(int roomId, CancellationToken cancellationToken = default);
}
