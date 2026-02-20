using Microsoft.EntityFrameworkCore;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Data;

public class ParcelTrackingDbContext : DbContext
{
    public ParcelTrackingDbContext(DbContextOptions<ParcelTrackingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();
    public DbSet<ParcelContentItem> ParcelContentItems => Set<ParcelContentItem>();
    public DbSet<DeliveryConfirmation> DeliveryConfirmations => Set<DeliveryConfirmation>();
    public DbSet<ParcelWatcher> ParcelWatchers => Set<ParcelWatcher>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParcelTrackingDbContext).Assembly);

    }

    
}