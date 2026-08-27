using System.Net;
using System.Net.Http.Json;
using StaySphere.Application.Reservations;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Api;

/// <summary>
/// Full-pipeline tests for the reservation endpoints: JSON model binding,
/// 201 / 400 / 404 / 409 status codes, the <c>Location</c> header, persistence,
/// and retrieval of a booking after it was created.
/// </summary>
public sealed class ReservationsEndpointsTests : IClassFixture<StaySphereApiFactory>, IAsyncLifetime
{
    private readonly StaySphereApiFactory _factory;
    private readonly HttpClient _client;

    public ReservationsEndpointsTests(StaySphereApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetReservationsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private object BookingBody(
        int roomId = SeededCatalog.StandardQueenRoom101Id,
        int checkInOffset = 10,
        int checkOutOffset = 13,
        int guestCount = 2,
        string? guestName = "Jordan Blake",
        string? guestEmail = "jordan.blake@example.com",
        string? specialRequests = null) => new
        {
            roomId,
            checkIn = _factory.Today.AddDays(checkInOffset).ToString("yyyy-MM-dd"),
            checkOut = _factory.Today.AddDays(checkOutOffset).ToString("yyyy-MM-dd"),
            guestCount,
            guestName,
            guestEmail,
            specialRequests,
        };

    // ---- POST /api/reservations : success -------------------------------

    [Fact]
    public async Task Create_Returns201WithLocationHeader_AndAConfirmationBody()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(specialRequests: "Late check-in around 11pm"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var confirmation = await response.Content.ReadFromJsonAsync<ReservationConfirmation>();
        Assert.NotNull(confirmation);
        Assert.StartsWith("STAY-", confirmation!.BookingReference);
        Assert.Equal("Confirmed", confirmation.Status);
        Assert.Equal(3, confirmation.Nights);
        Assert.Equal(SeededCatalog.StandardQueenPrice * 3, confirmation.TotalPrice);
        Assert.Equal("Late check-in around 11pm", confirmation.SpecialRequests);
    }

    [Fact]
    public async Task Create_ThenGetByReference_ReturnsTheSameBooking()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/reservations", BookingBody());
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationConfirmation>();

        var fetched = await _client.GetFromJsonAsync<ReservationConfirmation>(
            $"/api/reservations/{created!.BookingReference}");

        Assert.Equal(created.BookingReference, fetched!.BookingReference);
        Assert.Equal(created.RoomNumber, fetched.RoomNumber);
        Assert.Equal(created.CheckIn, fetched.CheckIn);
        Assert.Equal(created.TotalPrice, fetched.TotalPrice);
    }

    [Fact]
    public async Task Create_SerializesConfirmationFieldsAsCamelCase()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations", BookingBody());
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"bookingReference\"", json);
        Assert.Contains("\"pricePerNight\"", json);
        Assert.Contains("\"totalPrice\"", json);
        Assert.Contains("\"checkIn\"", json);
    }

    // ---- POST /api/reservations : validation / not found --------------

    [Fact]
    public async Task Create_Returns404_WhenTheRoomDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations", BookingBody(roomId: 9999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("NotFound", body!.Error);
    }

    [Fact]
    public async Task Create_Returns400_WhenCheckInIsInThePast()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(checkInOffset: -1, checkOutOffset: 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("ValidationFailed", body!.Error);
        Assert.Contains("checkIn", body.Errors!.Keys);
    }

    [Fact]
    public async Task Create_Returns400_WhenGuestCountExceedsRoomCapacity()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(roomId: SeededCatalog.StandardQueenRoom101Id, guestCount: 3));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Contains("guestCount", body!.Errors!.Keys);
    }

    [Fact]
    public async Task Create_Returns400WithFieldErrors_WhenGuestDetailsAreInvalid()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(guestName: "A", guestEmail: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Contains("guestName", body!.Errors!.Keys);
        Assert.Contains("guestEmail", body.Errors.Keys);
    }

    [Fact]
    public async Task Create_Returns400_WhenCheckInIsNotAValidDate()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations", new
        {
            roomId = SeededCatalog.StandardQueenRoom101Id,
            checkIn = "not-a-date",
            checkOut = "2026-06-15",
            guestCount = 2,
            guestName = "Jordan Blake",
            guestEmail = "jordan.blake@example.com",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- POST /api/reservations : booking conflict -------------------

    [Fact]
    public async Task Create_Returns409BookingConflict_WhenTheRoomIsAlreadyBookedForOverlappingDates()
    {
        var first = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(checkInOffset: 10, checkOutOffset: 13));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(checkInOffset: 12, checkOutOffset: 15));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("BookingConflict", body!.Error);
    }

    [Fact]
    public async Task Create_Allows_TwoBookingsForTheSameRoomOnAdjacentDates()
    {
        var first = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(checkInOffset: 10, checkOutOffset: 13));
        var second = await _client.PostAsJsonAsync("/api/reservations",
            BookingBody(checkInOffset: 13, checkOutOffset: 16));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // ---- GET /api/reservations/{reference} --------------------------

    [Fact]
    public async Task GetByReference_Returns404WithEnvelope_ForAnUnknownReference()
    {
        var response = await _client.GetAsync("/api/reservations/STAY-NOPE0000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("NotFound", body!.Error);
    }
}
