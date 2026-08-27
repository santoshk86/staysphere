using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StaySphere.Domain;

namespace StaySphere.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingReference).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.BookingReference).IsUnique();

        builder.Property(x => x.GuestName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.GuestEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.SpecialRequests).HasMaxLength(1000);
        builder.Property(x => x.GuestCount).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TotalPrice)
            .HasConversion<MoneyConverter>()
            .HasColumnName("TotalPriceCents")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // DateRange is an owned value object mapped to two columns on the Reservations table.
        builder.OwnsOne(x => x.Stay, stay =>
        {
            stay.Property(p => p.Start).HasColumnName("CheckIn").IsRequired();
            stay.Property(p => p.End).HasColumnName("CheckOut").IsRequired();
            stay.HasIndex(p => new { p.Start, p.End });
        });
        builder.Navigation(x => x.Stay).IsRequired();

        builder.HasOne(x => x.Room)
            .WithMany(r => r.Reservations)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports the availability query: filter by room + status, then by date interval.
        builder.HasIndex(x => new { x.RoomId, x.Status });
    }
}
