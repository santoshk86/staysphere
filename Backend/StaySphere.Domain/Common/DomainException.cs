namespace StaySphere.Domain.Common;

/// <summary>
/// Base type for violations of domain invariants and business rules.
/// The API layer maps these to appropriate HTTP status codes.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

/// <summary>Raised when a domain invariant or business rule is violated (maps to HTTP 400).</summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}

/// <summary>Raised when a room cannot be booked for the requested dates (maps to HTTP 409).</summary>
public sealed class RoomUnavailableException : DomainException
{
    public RoomUnavailableException(string message) : base(message)
    {
    }
}
