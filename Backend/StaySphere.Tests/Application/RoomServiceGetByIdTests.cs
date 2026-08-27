using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Common;
using StaySphere.Application.Rooms;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Application;

public sealed class RoomServiceGetByIdTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private RoomService NewService() => new(_database.CreateContext(), NullLogger<RoomService>.Instance);

    [Fact]
    public async Task GetById_ReturnsRoom_WithTypePriceCapacityAndAmenities()
    {
        var room = await NewService().GetByIdAsync(SeededCatalog.FamilySuiteRoom301Id);

        Assert.Equal("301", room.RoomNumber);
        Assert.Equal("Family Suite", room.RoomType);
        Assert.Equal(SeededCatalog.FamilySuitePrice, room.PricePerNight);
        Assert.Equal(SeededCatalog.FamilySuiteCapacity, room.MaxGuests);
        Assert.NotEmpty(room.Amenities);
        Assert.Equal(room.Amenities.OrderBy(a => a), room.Amenities); // alphabetical
    }

    [Fact]
    public async Task GetById_Throws_NotFound_WhenRoomDoesNotExist()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewService().GetByIdAsync(9999));
    }
}
