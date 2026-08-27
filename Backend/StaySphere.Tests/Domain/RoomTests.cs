using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Domain;

public class RoomTests
{
    [Fact]
    public void Constructor_SetsRoomNumberAndType()
    {
        var type = Build.RoomType(name: "Deluxe King");

        var room = new Room("201", type);

        Assert.Equal("201", room.RoomNumber);
        Assert.Same(type, room.RoomType);
    }

    [Fact]
    public void Constructor_TrimsRoomNumber()
    {
        var room = new Room("  201  ", Build.RoomType());

        Assert.Equal("201", room.RoomNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenRoomNumberIsBlank(string? roomNumber)
    {
        Assert.Throws<BusinessRuleViolationException>(() => new Room(roomNumber!, Build.RoomType()));
    }

    [Fact]
    public void Constructor_Throws_WhenRoomTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Room("201", null!));
    }

    [Fact]
    public void NewRoom_HasNoReservations()
    {
        var room = new Room("201", Build.RoomType());

        Assert.Empty(room.Reservations);
    }
}
