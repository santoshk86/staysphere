using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace StaySphere.Infrastructure.Persistence.Configurations;

/// <summary>
/// Stores monetary <see cref="decimal"/> values as integer minor units (cents).
/// SQLite has no decimal type; EF's default maps decimal to TEXT, which sorts
/// lexicographically and breaks <c>ORDER BY price</c>. Integer cents keep money
/// exact and correctly ordered.
/// </summary>
internal sealed class MoneyConverter : ValueConverter<decimal, long>
{
    public MoneyConverter()
        : base(
            value => (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero),
            cents => cents / 100m)
    {
    }
}
