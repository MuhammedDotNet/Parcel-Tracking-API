using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data.Configurations
{
    public class ParcelContentItemConfiguration : IEntityTypeConfiguration<ParcelContentItem>
    {
        public void Configure(EntityTypeBuilder<ParcelContentItem> builder)
        {
            builder.HasKey(ci => ci.Id);

            builder.Property(ci => ci.Id)
                .ValueGeneratedOnAdd();

            builder.HasIndex(ci => ci.ParcelId);
            builder.HasIndex(ci => ci.HsCode);

            builder.Property(ci => ci.UnitValue)
                .HasPrecision(12, 2);

            builder.Property(ci => ci.Weight)
                .HasPrecision(10, 2);
        }
    }
}
