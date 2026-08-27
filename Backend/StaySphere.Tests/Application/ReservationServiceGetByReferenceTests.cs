using Microsoft.Extensions.Logging.Abstractions;
using StaySphere.Application.Common;
using StaySphere.Application.Reservations;
using StaySphere.Tests.TestSupport;

namespace StaySphere.Tests.Application;

public sealed class ReservationServiceGetByReferenceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedClock _clock = new(Today);

    public void Dispose() => _database.Dispose();

    private ReservationService NewService()
        => new(_database.CreateContext(), _clock, new FakeBookingReferenceGenerator(),
            NullLogger<ReservationService>.Instance);

    [Fact]
    public async Task GetByReference_ReturnsTheFullConfirmation_ForABookingThatWasCreated()
    {
        var created = await NewService().CreateAsync(new CreateReservationCommand(
            SeededCatalog.FamilySuiteRoom301Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 14),
            3, "Jordan Blake", "jordan.blake@example.com", "Cot for the baby, please."));

        var fetched = await NewService().GetByReferenceAsync(created.BookingReference);

        Assert.Equal(created.BookingReference, fetched.BookingReference);
        Assert.Equal("Jordan Blake", fetched.GuestName);
        Assert.Equal("jordan.blake@example.com", fetched.GuestEmail);
        Assert.Equal("Cot for the baby, please.", fetched.SpecialRequests);
        Assert.Equal("301", fetched.RoomNumber);
        Assert.Equal("Family Suite", fetched.RoomType);
        Assert.Equal(4, fetched.Nights);
        Assert.Equal(3, fetched.GuestCount);
        Assert.Equal(SeededCatalog.FamilySuitePrice * 4, fetched.TotalPrice);
        Assert.Equal("Confirmed", fetched.Status);
        Assert.NotEmpty(fetched.Amenities);
    }

    [Fact]
    public async Task GetByReference_Throws_NotFound_WhenReferenceIsUnknown()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => NewService().GetByReferenceAsync("STAY-DOESNOTEXIST"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByReference_Throws_Validation_WhenReferenceIsBlank(string reference)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => NewService().GetByReferenceAsync(reference));
    }

    [Fact]
    public async Task GetByReference_IsCaseSensitive()
    {
        // Documents current behaviour: the lookup uses an ordinal string match.
        var created = await NewService().CreateAsync(new CreateReservationCommand(
            SeededCatalog.StandardQueenRoom101Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12),
            2, "Jordan Blake", "jordan.blake@example.com", null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => NewService().GetByReferenceAsync(created.BookingReference.ToLowerInvariant()));
    }
}
