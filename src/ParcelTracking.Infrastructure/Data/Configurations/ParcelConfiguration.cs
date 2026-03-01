using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data.Configurations;

public class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        // Unique tracking number
        builder.HasIndex(p => p.TrackingNumber)
            .IsUnique();

        // Shipper address (many-to-one)
        builder.HasOne(p => p.ShipperAddress)
            .WithMany()
            .HasForeignKey(p => p.ShipperAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // Recipient address (many-to-one)
        builder.HasOne(p => p.RecipientAddress)
            .WithMany()
            .HasForeignKey(p => p.RecipientAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tracking events (one-to-many)
        builder.HasMany(p => p.TrackingEvents)
            .WithOne(te => te.Parcel)
            .HasForeignKey(te => te.ParcelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Content items (one-to-many)
        builder.HasMany(p => p.ContentItems)
            .WithOne(ci => ci.Parcel)
            .HasForeignKey(ci => ci.ParcelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Delivery confirmation (one-to-one optional)
        builder.HasOne(p => p.DeliveryConfirmation)
            .WithOne(dc => dc.Parcel)
            .HasForeignKey<DeliveryConfirmation>(dc => dc.ParcelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Parcel watchers (many-to-many)
        builder.HasMany(p => p.Watchers)
            .WithMany(w => w.Parcels);

        // Indexes for common queries
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ServiceType);
        builder.HasIndex(p => p.CreatedAt);

        // Composite indexes for cursor-based pagination with deterministic ordering
        builder.HasIndex(p => new { p.CreatedAt, p.Id })
            .HasDatabaseName("IX_Parcels_CreatedAt_Id");

        builder.HasIndex(p => new { p.EstimatedDeliveryDate, p.Id })
            .HasDatabaseName("IX_Parcels_EstimatedDeliveryDate_Id");

        builder.HasIndex(p => new { p.Status, p.Id })
            .HasDatabaseName("IX_Parcels_Status_Id");

        // Decimal precision
        builder.Property(p => p.Weight)
            .HasPrecision(10, 2);

        builder.Property(p => p.Length)
            .HasPrecision(10, 2);

        builder.Property(p => p.Width)
            .HasPrecision(10, 2);

        builder.Property(p => p.Height)
            .HasPrecision(10, 2);

        builder.Property(p => p.DeclaredValue)
            .HasPrecision(12, 2);
    }
}