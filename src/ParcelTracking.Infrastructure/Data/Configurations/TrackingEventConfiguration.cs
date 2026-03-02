using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data.Configurations
{
    public class TrackingEventConfiguration : IEntityTypeConfiguration<TrackingEvent>
    {
        public void Configure(EntityTypeBuilder<TrackingEvent> builder)
        {
            builder.HasKey(te => te.Id);

            builder.Property(te => te.Id)
                .ValueGeneratedOnAdd();

            // Foreign key relationship to Parcel
            builder.HasOne(te => te.Parcel)
                .WithMany(p => p.TrackingEvents)
                .HasForeignKey(te => te.ParcelId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite index on (ParcelId, Timestamp) for efficient queries
            builder.HasIndex(te => new { te.ParcelId, te.Timestamp });

            // Additional indexes for common queries
            builder.HasIndex(te => te.EventType);

            // Composite index for analytics queries
            builder.HasIndex(te => new { te.EventType, te.Timestamp })
                .HasDatabaseName("IX_TrackingEvents_EventType_Timestamp");

            // Column constraints - Required fields
            builder.Property(te => te.Timestamp)
                .IsRequired();

            builder.Property(te => te.EventType)
                .IsRequired();

            builder.Property(te => te.Description)
                .IsRequired()
                .HasMaxLength(500);

            // Column constraints - Optional fields with max lengths
            builder.Property(te => te.LocationCity)
                .HasMaxLength(100);

            builder.Property(te => te.LocationState)
                .HasMaxLength(100);

            builder.Property(te => te.LocationCountry)
                .HasMaxLength(100);

            builder.Property(te => te.DelayReason)
                .HasMaxLength(500);
        }
    }
}
