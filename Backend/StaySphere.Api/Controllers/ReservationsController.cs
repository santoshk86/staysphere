using Microsoft.AspNetCore.Mvc;
using StaySphere.Api.Contracts;
using StaySphere.Application.Reservations;

namespace StaySphere.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Produces("application/json")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservations;

    public ReservationsController(IReservationService reservations)
    {
        _reservations = reservations;
    }

    /// <summary>Create a reservation. Availability is re-checked authoritatively here.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationConfirmation), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationConfirmation>> Create(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateReservationCommand(
            request.RoomId,
            request.CheckIn,
            request.CheckOut,
            request.GuestCount,
            request.GuestName,
            request.GuestEmail,
            request.SpecialRequests);

        var confirmation = await _reservations.CreateAsync(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetByReference),
            new { reference = confirmation.BookingReference },
            confirmation);
    }

    /// <summary>Retrieve a booking confirmation by its reference.</summary>
    [HttpGet("{reference}")]
    [ProducesResponseType(typeof(ReservationConfirmation), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationConfirmation>> GetByReference(string reference, CancellationToken cancellationToken)
    {
        var confirmation = await _reservations.GetByReferenceAsync(reference, cancellationToken);
        return Ok(confirmation);
    }
}
