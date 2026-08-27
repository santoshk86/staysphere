using StaySphere.Domain;
using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Domain;

public class AmenityTests
{
    [Fact]
    public void Constructor_TrimsName()
    {
        var amenity = new Amenity("  Free Wi-Fi  ");

        Assert.Equal("Free Wi-Fi", amenity.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenNameIsBlank(string? name)
    {
        Assert.Throws<BusinessRuleViolationException>(() => new Amenity(name!));
    }
}

public class RoomTypeAmenityTests
{
    [Fact]
    public void Constructor_LinksRoomTypeAndAmenity()
    {
        var type = Build.RoomType();
        var amenity = new Amenity("Balcony");

        var link = new RoomTypeAmenity(type, amenity);

        Assert.Same(type, link.RoomType);
        Assert.Same(amenity, link.Amenity);
    }

    [Fact]
    public void Constructor_Throws_WhenRoomTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RoomTypeAmenity(null!, new Amenity("Balcony")));
    }

    [Fact]
    public void Constructor_Throws_WhenAmenityIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RoomTypeAmenity(Build.RoomType(), null!));
    }
}
