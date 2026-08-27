using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StaySphere.Application.Rooms;
using StaySphere.Infrastructure.Persistence;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Api;

/// <summary>
/// Full-pipeline tests for the room endpoints: routing, query-string model
/// binding, status codes, the error envelope and JSON serialization, backed by a
/// real (in-memory) SQLite database.
/// </summary>
public sealed class RoomsEndpointsTests : IClassFixture<StaySphereApiFactory>, IAsyncLifetime
{
    private readonly StaySphereApiFactory _factory;
    private readonly HttpClient _client;

    public RoomsEndpointsTests(StaySphereApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetReservationsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private string Range(int fromOffset, int toOffset) =>
        $"checkIn={_factory.Today.AddDays(fromOffset):yyyy-MM-dd}&checkOut={_factory.Today.AddDays(toOffset):yyyy-MM-dd}";

    // ---- GET /api/rooms/search --------------------------------------------

    [Fact]
    public async Task Search_ReturnsOkWithRooms_ForAValidQuery()
    {
        var response = await _client.GetAsync($"/api/rooms/search?{Range(10, 13)}&guests=2");

        response.EnsureSuccessStatusCode();
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomDto>>();

        Assert.NotNull(rooms);
        Assert.Equal(SeededCatalog.RoomCount, rooms!.Count);
        Assert.Equal(SeededCatalog.RoomNumbersByPriceThenNumber, rooms.Select(r => r.RoomNumber));
    }

    [Fact]
    public async Task Search_SerializesRoomFieldsAsCamelCase()
    {
        var json = await _client.GetStringAsync($"/api/rooms/search?{Range(10, 13)}&guests=2");

        Assert.Contains("\"roomNumber\"", json);
        Assert.Contains("\"pricePerNight\"", json);
        Assert.Contains("\"maxGuests\"", json);
        Assert.Contains("\"amenities\"", json);
    }

    [Fact]
    public async Task Search_ExcludesARoom_WithAnOverlappingConfirmedReservation()
    {
        await _factory.WithDbAsync(async (StaySphereDbContext db) =>
        {
            await new ReservationSeeder(db).AddConfirmedAsync(
                SeededCatalog.StandardQueenRoom101Id,
                _factory.Today.AddDays(10), _factory.Today.AddDays(13));
            return true;
        });

        var rooms = await _client.GetFromJsonAsync<List<RoomDto>>(
            $"/api/rooms/search?{Range(11, 14)}&guests=2");

        Assert.DoesNotContain(rooms!, r => r.RoomNumber == "101");
        Assert.Contains(rooms!, r => r.RoomNumber == "102");
    }

    [Fact]
    public async Task Search_Returns400_WhenRequiredParametersAreMissing()
    {
        var response = await _client.GetAsync("/api/rooms/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("ValidationFailed", body!.Error);
        Assert.Contains("checkIn", body.Errors!.Keys);
        Assert.Contains("checkOut", body.Errors.Keys);
        Assert.Contains("guests", body.Errors.Keys);
    }

    [Fact]
    public async Task Search_Returns400_WhenCheckOutIsNotAfterCheckIn()
    {
        var response = await _client.GetAsync($"/api/rooms/search?{Range(13, 13)}&guests=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("ValidationFailed", body!.Error);
    }

    [Fact]
    public async Task Search_Returns400_WhenGuestCountIsZero()
    {
        var response = await _client.GetAsync($"/api/rooms/search?{Range(10, 13)}&guests=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_Returns400_WhenADateCannotBeParsed()
    {
        var response = await _client.GetAsync("/api/rooms/search?checkIn=not-a-date&checkOut=2026-06-15&guests=2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- GET /api/rooms/{roomId} ---------------------------------------

    [Fact]
    public async Task GetById_ReturnsOk_ForAnExistingRoom()
    {
        var room = await _client.GetFromJsonAsync<RoomDto>($"/api/rooms/{SeededCatalog.FamilySuiteRoom301Id}");

        Assert.Equal("301", room!.RoomNumber);
        Assert.Equal("Family Suite", room.RoomType);
        Assert.Equal(SeededCatalog.FamilySuiteCapacity, room.MaxGuests);
    }

    [Fact]
    public async Task GetById_Returns404WithEnvelope_ForAnUnknownRoom()
    {
        var response = await _client.GetAsync("/api/rooms/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("NotFound", body!.Error);
    }

    [Fact]
    public async Task GetById_Returns404_WhenTheIdIsNotAnInteger()
    {
        var response = await _client.GetAsync("/api/rooms/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
