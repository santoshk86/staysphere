using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Configurations;

internal sealed class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.ToTable("RoomTypes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.PricePerNight)
            .HasConversion<MoneyConverter>()
            .HasColumnName("PricePerNightCents")
            .IsRequired();
        builder.Property(x => x.MaxGuests).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();

        builder.Navigation(x => x.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.RoomTypeAmenities).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Seed values use the CLR property names; EF applies the money converter.
        builder.HasData(
            new { Id = 1, Name = "Standard Queen", Description = "A comfortable room with a queen bed, ideal for solo travellers or couples.", PricePerNight = 99.00m, MaxGuests = 2, ImageUrl = "/images/rooms/standard-queen.svg" },
            new { Id = 2, Name = "Deluxe King", Description = "A spacious room with a king bed, seating area and city views.", PricePerNight = 159.00m, MaxGuests = 2, ImageUrl = "/images/rooms/deluxe-king.svg" },
            new { Id = 3, Name = "Family Suite", Description = "Two-room suite with a king bed and a separate lounge with a sofa bed.", PricePerNight = 249.00m, MaxGuests = 4, ImageUrl = "/images/rooms/family-suite.svg" },
            new { Id = 4, Name = "Executive Suite", Description = "Premium corner suite with a king bed, work area, lounge and premium amenities.", PricePerNight = 399.00m, MaxGuests = 3, ImageUrl = "/images/rooms/executive-suite.svg" });
    }
}
