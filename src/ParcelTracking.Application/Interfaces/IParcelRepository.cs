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
}
