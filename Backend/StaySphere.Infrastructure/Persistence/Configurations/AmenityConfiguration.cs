using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Configurations;

internal sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("Amenities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Navigation(x => x.RoomTypeAmenities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            new { Id = 1, Name = "Free Wi-Fi" },
            new { Id = 2, Name = "Air conditioning" },
            new { Id = 3, Name = "Flat-screen TV" },
            new { Id = 4, Name = "Mini bar" },
            new { Id = 5, Name = "Coffee machine" },
            new { Id = 6, Name = "Safe" },
            new { Id = 7, Name = "City view" },
            new { Id = 8, Name = "Balcony" },
            new { Id = 9, Name = "Bathtub" },
            new { Id = 10, Name = "Lounge access" });
    }
}
