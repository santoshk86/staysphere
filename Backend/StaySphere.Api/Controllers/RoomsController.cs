using Microsoft.AspNetCore.Mvc;
using StaySphere.Api.Contracts;
using StaySphere.Application.Rooms;

namespace StaySphere.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _rooms;

    public RoomsController(IRoomService rooms)
    {
        _rooms = rooms;
    }

    /// <summary>Search rooms available for the whole date range with capacity for the guest count.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> Search(
        [FromQuery] DateOnly? checkIn,
        [FromQuery] DateOnly? checkOut,
        [FromQuery] int? guests,
        CancellationToken cancellationToken)
    {
        var results = await _rooms.SearchAsync(new RoomSearchQuery(checkIn, checkOut, guests), cancellationToken);
        return Ok(results);
    }

    /// <summary>Full details for a single physical room.</summary>
    [HttpGet("{roomId:int}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> GetById(int roomId, CancellationToken cancellationToken)
    {
        var room = await _rooms.GetByIdAsync(roomId, cancellationToken);
        return Ok(room);
    }
}
