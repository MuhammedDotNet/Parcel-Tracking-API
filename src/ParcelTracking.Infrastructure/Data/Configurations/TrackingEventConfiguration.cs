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

            builder.HasIndex(te => te.ParcelId);
            builder.HasIndex(te => te.Timestamp);
            builder.HasIndex(te => te.EventType);
        }
    }
}
