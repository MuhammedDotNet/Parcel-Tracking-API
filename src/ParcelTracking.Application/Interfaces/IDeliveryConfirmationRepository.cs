using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

/// <summary>
/// Repository interface for DeliveryConfirmation data access.
/// </summary>
public interface IDeliveryConfirmationRepository
{
    /// <summary>Gets the delivery confirmation for a specific parcel.</summary>
    Task<DeliveryConfirmation?> GetByParcelIdAsync(int parcelId, CancellationToken ct = default);

    /// <summary>Checks whether a delivery confirmation exists for a specific parcel.</summary>
    Task<bool> ExistsForParcelAsync(int parcelId, CancellationToken ct = default);

    /// <summary>Adds a new delivery confirmation to the data store.</summary>
    Task AddAsync(DeliveryConfirmation confirmation, CancellationToken ct = default);

    /// <summary>Persists all pending changes to the data store.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
