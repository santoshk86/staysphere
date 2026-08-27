using System.Text.Json;
using StaySphere.Api.Contracts;
using StaySphere.Application.Common;
using StaySphere.Domain.Common;

namespace StaySphere.Api.Middleware;

/// <summary>
/// Single place that turns exceptions into the <see cref="ApiErrorResponse"/> envelope
/// with the right status code, so controllers stay free of try/catch.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const int StatusClientClosedRequest = 499;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (status, error, message, errors) = exception switch
        {
            ValidationException ex => (
                StatusCodes.Status400BadRequest, "ValidationFailed", ex.Message, ex.Errors),
            NotFoundException ex => (
                StatusCodes.Status404NotFound, "NotFound", ex.Message, (IReadOnlyDictionary<string, string[]>?)null),
            RoomUnavailableException ex => (
                StatusCodes.Status409Conflict, "BookingConflict", ex.Message, null),
            DomainException ex => (
                StatusCodes.Status400BadRequest, "BusinessRuleViolation", ex.Message, null),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (
                StatusClientClosedRequest, "ClientClosedRequest", "The request was cancelled.", null),
            _ => (
                StatusCodes.Status500InternalServerError, "ServerError", "An unexpected error occurred.", null)
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception ({TraceId})", traceId);
        }
        else if (status == StatusCodes.Status409Conflict)
        {
            _logger.LogWarning("Booking conflict ({TraceId}): {Message}", traceId, message);
        }
        else
        {
            _logger.LogInformation("Request rejected ({TraceId}): {Status} {Message}", traceId, status, message);
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var payload = new ApiErrorResponse(status, error, message, errors, traceId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
