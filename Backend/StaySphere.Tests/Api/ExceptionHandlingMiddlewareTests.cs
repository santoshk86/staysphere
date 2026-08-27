using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Api.Middleware;
using StaySphere.Application.Common;
using StaySphere.Domain.Common;

namespace StaySphere.Tests.Api;

/// <summary>
/// Directly exercises the exception-to-HTTP mapping in the centralized error
/// middleware. The full pipeline tests cover the reachable paths (400/404/409);
/// this covers the whole table, including the generic <see cref="DomainException"/>
/// and unexpected-exception cases, and asserts internal detail is not leaked on a
/// 500.
/// </summary>
public sealed class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, ErrorEnvelope Body)> Handle(Exception thrown)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-abc" };
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = JsonSerializer.Deserialize<ErrorEnvelope>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task PassesThrough_WhenNoExceptionIsThrown()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; },
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task ValidationException_MapsTo400_WithFieldErrors()
    {
        var (status, body) = await Handle(new ValidationException("guestEmail", "Guest email is not valid."));

        Assert.Equal(400, status);
        Assert.Equal("ValidationFailed", body.Error);
        Assert.Contains("guestEmail", body.Errors!.Keys);
        Assert.Equal("trace-abc", body.TraceId);
    }

    [Fact]
    public async Task NotFoundException_MapsTo404()
    {
        var (status, body) = await Handle(new NotFoundException("Room 5 was not found."));

        Assert.Equal(404, status);
        Assert.Equal("NotFound", body.Error);
        Assert.Null(body.Errors);
    }

    [Fact]
    public async Task RoomUnavailableException_MapsTo409_BookingConflict()
    {
        var (status, body) = await Handle(new RoomUnavailableException("Room is taken."));

        Assert.Equal(409, status);
        Assert.Equal("BookingConflict", body.Error);
    }

    [Fact]
    public async Task GenericDomainException_MapsTo400_BusinessRuleViolation()
    {
        var (status, body) = await Handle(new BusinessRuleViolationException("A stay must be at least one night."));

        Assert.Equal(400, status);
        Assert.Equal("BusinessRuleViolation", body.Error);
        Assert.Equal("A stay must be at least one night.", body.Message);
    }

    [Fact]
    public async Task CancelledRequest_MapsToClientClosedRequest499()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationCanceledException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_MapsTo500_AndDoesNotLeakInternalDetail()
    {
        var (status, body) = await Handle(new InvalidOperationException("connection string secret xyz"));

        Assert.Equal(500, status);
        Assert.Equal("ServerError", body.Error);
        Assert.DoesNotContain("secret", body.Message, StringComparison.OrdinalIgnoreCase);
    }
}
