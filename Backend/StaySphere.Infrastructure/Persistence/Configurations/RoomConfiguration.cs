using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoomNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.RoomNumber).IsUnique();

        builder.HasOne(x => x.RoomType)
            .WithMany(t => t.Rooms)
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Reservations).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Physical inventory: a few rooms per type so availability is meaningful.
        builder.HasData(
            new { Id = 1, RoomNumber = "101", RoomTypeId = 1 },
            new { Id = 2, RoomNumber = "102", RoomTypeId = 1 },
            new { Id = 3, RoomNumber = "103", RoomTypeId = 1 },
            new { Id = 4, RoomNumber = "201", RoomTypeId = 2 },
            new { Id = 5, RoomNumber = "202", RoomTypeId = 2 },
            new { Id = 6, RoomNumber = "301", RoomTypeId = 3 },
            new { Id = 7, RoomNumber = "302", RoomTypeId = 3 },
            new { Id = 8, RoomNumber = "401", RoomTypeId = 4 });
    }
}
