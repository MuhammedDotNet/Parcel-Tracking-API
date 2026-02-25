using Microsoft.EntityFrameworkCore;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Infrastructure.Repositories;

public sealed class ParcelRepository : IParcelRepository
{
    private readonly ParcelTrackingDbContext _db;

    public ParcelRepository(ParcelTrackingDbContext db) => _db = db;

    public async Task AddAsync(Parcel parcel, CancellationToken ct)
        => await _db.Parcels.AddAsync(parcel, ct);

    public Task AddTrackingEventAsync(TrackingEvent trackingEvent, CancellationToken ct)
    {
        _db.TrackingEvents.Add(trackingEvent);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);

    public Task<bool> AddressExistsAsync(int addressId, CancellationToken ct)
        => _db.Addresses.AnyAsync(a => a.Id == addressId, ct);
}
