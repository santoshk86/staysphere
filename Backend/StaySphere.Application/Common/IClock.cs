namespace StaySphere.Application.Common;

/// <summary>Abstraction over the system clock so time-dependent rules stay testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}
