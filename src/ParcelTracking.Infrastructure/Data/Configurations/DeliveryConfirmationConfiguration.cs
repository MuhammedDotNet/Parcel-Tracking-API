using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data.Configurations
{
    public class DeliveryConfirmationConfiguration : IEntityTypeConfiguration<DeliveryConfirmation>
    {
        public void Configure(EntityTypeBuilder<DeliveryConfirmation> builder)
        {
            builder.HasKey(dc => dc.Id);

            builder.Property(dc => dc.Id)
                .ValueGeneratedOnAdd();

            // Ensure one confirmation per parcel at the database level
            builder.HasIndex(dc => dc.ParcelId)
                .IsUnique();

            builder.Property(dc => dc.ReceivedBy)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(dc => dc.DeliveryLocation)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(dc => dc.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
