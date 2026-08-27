using StaySphere.Domain.Common;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Domain;

public class RoomTypeTests
{
    [Fact]
    public void Constructor_SetsProvidedValues()
    {
        var type = Build.RoomType(
            name: "Deluxe King",
            description: "Spacious room with a king bed.",
            pricePerNight: 159.00m,
            maxGuests: 2,
            imageUrl: "/images/rooms/deluxe-king.svg");

        Assert.Equal("Deluxe King", type.Name);
        Assert.Equal("Spacious room with a king bed.", type.Description);
        Assert.Equal(159.00m, type.PricePerNight);
        Assert.Equal(2, type.MaxGuests);
        Assert.Equal("/images/rooms/deluxe-king.svg", type.ImageUrl);
    }

    [Fact]
    public void Constructor_TrimsNameAndDescription()
    {
        var type = Build.RoomType(name: "  Deluxe King  ", description: "  Nice room.  ");

        Assert.Equal("Deluxe King", type.Name);
        Assert.Equal("Nice room.", type.Description);
    }

    [Fact]
    public void Constructor_FallsBackToPlaceholderImage_WhenImageUrlBlank()
    {
        var type = Build.RoomType(imageUrl: "   ");

        Assert.Equal("/images/room-placeholder.svg", type.ImageUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_WhenNameIsBlank(string? name)
    {
        Assert.Throws<BusinessRuleViolationException>(() => Build.RoomType(name: name!));
    }

    [Fact]
    public void Constructor_Throws_WhenPriceIsZeroOrNegative()
    {
        Assert.Throws<BusinessRuleViolationException>(() => Build.RoomType(pricePerNight: 0m));
        Assert.Throws<BusinessRuleViolationException>(() => Build.RoomType(pricePerNight: -50m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_WhenMaxGuestsIsNotPositive(int maxGuests)
    {
        Assert.Throws<BusinessRuleViolationException>(() => Build.RoomType(maxGuests: maxGuests));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void CalculatePrice_ReturnsPricePerNightTimesNights(int nights)
    {
        var type = Build.RoomType(pricePerNight: 149.00m);

        Assert.Equal(149.00m * nights, type.CalculatePrice(nights));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void CalculatePrice_Throws_WhenNightsIsNotPositive(int nights)
    {
        Assert.Throws<BusinessRuleViolationException>(() => Build.RoomType().CalculatePrice(nights));
    }
}
