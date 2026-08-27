namespace StaySphere.Application.Reservations;

public interface IReservationService
{
    /// <summary>
    /// Runs the full booking workflow: validate, load room, check capacity, re-check
    /// availability inside a serialized transaction, persist, and return confirmation.
    /// </summary>
    Task<ReservationConfirmation> CreateAsync(CreateReservationCommand command, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a confirmation by its public booking reference.</summary>
    Task<ReservationConfirmation> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
}
