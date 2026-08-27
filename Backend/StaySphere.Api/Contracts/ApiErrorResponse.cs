namespace StaySphere.Api.Contracts;

/// <summary>Consistent error envelope returned for every non-success response.</summary>
public sealed record ApiErrorResponse(
    int Status,
    string Error,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? TraceId = null);
