namespace StaySphere.Tests.Api;

/// <summary>Mirror of the API's error envelope for deserialization in tests.</summary>
public sealed record ErrorEnvelope(
    int Status,
    string Error,
    string Message,
    Dictionary<string, string[]>? Errors,
    string? TraceId);
