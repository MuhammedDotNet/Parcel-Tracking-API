using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data.Configurations
{
    public class ParcelWatcherConfiguration : IEntityTypeConfiguration<ParcelWatcher>
    {
        public void Configure(EntityTypeBuilder<ParcelWatcher> builder)
        {
            builder.HasKey(w => w.Id);

            builder.Property(w => w.Id)
                .ValueGeneratedOnAdd();

            builder.HasIndex(w => w.Email);
        }
    }
}
