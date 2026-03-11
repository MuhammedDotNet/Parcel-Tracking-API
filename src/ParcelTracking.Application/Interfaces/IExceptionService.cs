using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

/// <summary>
/// Service interface for delivery exception handling and retry workflows.
/// </summary>
public interface IExceptionService
{
    /// <summary>
    /// Reports a delivery exception, transitioning the parcel to Exception status,
    /// incrementing the delivery attempt counter, and creating a tracking event.
    /// </summary>
    Task<Parcel> ReportExceptionAsync(int parcelId, ReportExceptionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Schedules a redelivery attempt for a parcel in Exception status.
    /// If the maximum delivery attempts have been reached, the parcel is auto-returned.
    /// </summary>
    /// <returns>A tuple: the updated parcel and a boolean indicating if it was auto-returned.</returns>
    Task<(Parcel Parcel, bool AutoReturned)> RetryDeliveryAsync(int parcelId, RetryDeliveryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all parcels currently in Exception status, ordered by UpdatedAt ascending.
    /// Includes shipper and recipient address data.
    /// </summary>
    Task<List<Parcel>> GetExceptionParcelsAsync(CancellationToken ct = default);
}
