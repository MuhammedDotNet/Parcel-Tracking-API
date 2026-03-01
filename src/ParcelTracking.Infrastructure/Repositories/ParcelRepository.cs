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

    public Task<Parcel?> GetByIdWithDetailsAsync(int id, CancellationToken ct)
        => _db.Parcels
            .Include(p => p.ShipperAddress)
            .Include(p => p.RecipientAddress)
            .Include(p => p.ContentItems)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Parcel?> GetByTrackingNumberWithRecipientAsync(
        string trackingNumber, CancellationToken ct)
        => _db.Parcels
            .Include(p => p.RecipientAddress)
            .FirstOrDefaultAsync(p => p.TrackingNumber == trackingNumber, ct);

    public Task<bool> ParcelExistsAsync(int parcelId, CancellationToken ct)
        => _db.Parcels.AnyAsync(p => p.Id == parcelId, ct);

    public Task<TrackingEvent?> GetLatestTrackingEventAsync(int parcelId, CancellationToken ct)
        => _db.TrackingEvents
            .Where(e => e.ParcelId == parcelId)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(ct);

    public Task<Parcel?> GetByIdAsync(int parcelId, CancellationToken ct)
        => _db.Parcels.FirstOrDefaultAsync(p => p.Id == parcelId, ct);

    public async Task<List<TrackingEvent>> GetTrackingEventsAsync(
        int parcelId, 
        DateTimeOffset? from, 
        DateTimeOffset? to, 
        CancellationToken ct)
    {
        var query = _db.TrackingEvents.Where(e => e.ParcelId == parcelId);

        if (from.HasValue)
        {
            query = query.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Timestamp <= to.Value);
        }

        return await query.OrderBy(e => e.Timestamp).ToListAsync(ct);
    }

    public IQueryable<Parcel> GetQueryableWithAddresses()
        => _db.Parcels
            .Include(p => p.ShipperAddress)
            .Include(p => p.RecipientAddress);

    public Task<int> CountAsync(IQueryable<Parcel> query, CancellationToken ct)
        => query.CountAsync(ct);

    public Task<List<Parcel>> ToListAsync(IQueryable<Parcel> query, CancellationToken ct)
        => query.ToListAsync(ct);
}
