using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

/// <summary>
/// Service interface for delivery confirmation business logic.
/// </summary>
public interface IDeliveryConfirmationService
{
    /// <summary>
    /// Confirms delivery of a parcel by creating a DeliveryConfirmation record,
    /// auto-generating a TrackingEvent, and updating the Parcel status.
    /// </summary>
    /// <param name="trackingNumber">The parcel tracking number.</param>
    /// <param name="request">The delivery confirmation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created delivery confirmation summary.</returns>
    Task<DeliveryConfirmationResponse> ConfirmDeliveryAsync(
        string trackingNumber,
        ConfirmDeliveryRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the delivery confirmation details for a parcel, including
    /// the calculated isOnTime flag.
    /// </summary>
    /// <param name="trackingNumber">The parcel tracking number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full confirmation details, or null if not found.</returns>
    Task<DeliveryConfirmationDetailResponse?> GetDeliveryConfirmationAsync(
        string trackingNumber,
        CancellationToken ct = default);
}
