using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

public interface IParcelRepository
{
    Task AddAsync(Parcel parcel, CancellationToken ct);
    Task AddTrackingEventAsync(TrackingEvent trackingEvent, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<bool> AddressExistsAsync(int addressId, CancellationToken ct);
    Task<Parcel?> GetByIdWithDetailsAsync(int id, CancellationToken ct);
    Task<Parcel?> GetByTrackingNumberWithRecipientAsync(string trackingNumber, CancellationToken ct);
    Task<bool> ParcelExistsAsync(int parcelId, CancellationToken ct);
    Task<TrackingEvent?> GetLatestTrackingEventAsync(int parcelId, CancellationToken ct);
    Task<Parcel?> GetByIdAsync(int parcelId, CancellationToken ct);
    Task<List<TrackingEvent>> GetTrackingEventsAsync(int parcelId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    IQueryable<Parcel> GetQueryableWithAddresses();
    Task<int> CountAsync(IQueryable<Parcel> query, CancellationToken ct);
    Task<List<Parcel>> ToListAsync(IQueryable<Parcel> query, CancellationToken ct);
    Task<Parcel?> GetByIdWithRecipientAsync(int id, CancellationToken ct);
    Task<List<Parcel>> GetExceptionParcelsAsync(CancellationToken ct);
}
