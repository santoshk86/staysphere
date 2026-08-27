using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Configurations;

internal sealed class RoomTypeAmenityConfiguration : IEntityTypeConfiguration<RoomTypeAmenity>
{
    public void Configure(EntityTypeBuilder<RoomTypeAmenity> builder)
    {
        builder.ToTable("RoomTypeAmenities");
        builder.HasKey(x => new { x.RoomTypeId, x.AmenityId });

        builder.HasOne(x => x.RoomType)
            .WithMany(t => t.RoomTypeAmenities)
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Amenity)
            .WithMany(a => a.RoomTypeAmenities)
            .HasForeignKey(x => x.AmenityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            // Standard Queen
            new { RoomTypeId = 1, AmenityId = 1 },
            new { RoomTypeId = 1, AmenityId = 2 },
            new { RoomTypeId = 1, AmenityId = 3 },
            // Deluxe King
            new { RoomTypeId = 2, AmenityId = 1 },
            new { RoomTypeId = 2, AmenityId = 2 },
            new { RoomTypeId = 2, AmenityId = 3 },
            new { RoomTypeId = 2, AmenityId = 4 },
            new { RoomTypeId = 2, AmenityId = 5 },
            new { RoomTypeId = 2, AmenityId = 7 },
            // Family Suite
            new { RoomTypeId = 3, AmenityId = 1 },
            new { RoomTypeId = 3, AmenityId = 2 },
            new { RoomTypeId = 3, AmenityId = 3 },
            new { RoomTypeId = 3, AmenityId = 5 },
            new { RoomTypeId = 3, AmenityId = 6 },
            new { RoomTypeId = 3, AmenityId = 8 },
            new { RoomTypeId = 3, AmenityId = 9 },
            // Executive Suite
            new { RoomTypeId = 4, AmenityId = 1 },
            new { RoomTypeId = 4, AmenityId = 2 },
            new { RoomTypeId = 4, AmenityId = 3 },
            new { RoomTypeId = 4, AmenityId = 4 },
            new { RoomTypeId = 4, AmenityId = 5 },
            new { RoomTypeId = 4, AmenityId = 6 },
            new { RoomTypeId = 4, AmenityId = 7 },
            new { RoomTypeId = 4, AmenityId = 9 },
            new { RoomTypeId = 4, AmenityId = 10 });
    }
}
