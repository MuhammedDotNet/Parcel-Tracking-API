using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Services;

/// <summary>
/// Orchestrates the delivery confirmation workflow: creating a DeliveryConfirmation,
/// auto-generating a TrackingEvent with EventType.Delivered, and updating the Parcel status.
/// All changes are persisted in a single SaveChangesAsync call (transactional).
/// </summary>
public class DeliveryConfirmationService : IDeliveryConfirmationService
{
    private readonly IParcelRepository _parcelRepository;
    private readonly IDeliveryConfirmationRepository _confirmationRepository;

    public DeliveryConfirmationService(
        IParcelRepository parcelRepository,
        IDeliveryConfirmationRepository confirmationRepository)
    {
        _parcelRepository = parcelRepository;
        _confirmationRepository = confirmationRepository;
    }

    /// <inheritdoc/>
    public async Task<DeliveryConfirmationResponse> ConfirmDeliveryAsync(
        string trackingNumber,
        ConfirmDeliveryRequest request,
        CancellationToken ct = default)
    {
        // 1. Load parcel by tracking number with recipient address
        var parcel = await _parcelRepository.GetByTrackingNumberWithRecipientAsync(trackingNumber, ct);
        if (parcel is null)
        {
            throw new KeyNotFoundException($"Parcel with tracking number '{trackingNumber}' not found.");
        }

        // 2. Validate parcel status — Delivered means duplicate, others may be invalid
        if (parcel.Status == ParcelStatus.Delivered)
        {
            throw new InvalidOperationException(
                $"CONFLICT:Delivery confirmation already exists for parcel '{trackingNumber}'.");
        }

        if (parcel.Status != ParcelStatus.InTransit && parcel.Status != ParcelStatus.OutForDelivery)
        {
            throw new InvalidOperationException(
                $"Parcel status '{parcel.Status}' is not valid for delivery confirmation. " +
                $"Valid statuses: InTransit, OutForDelivery.");
        }

        // 3. Check for duplicate confirmation
        var alreadyExists = await _confirmationRepository.ExistsForParcelAsync(parcel.Id, ct);
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"CONFLICT:Delivery confirmation already exists for parcel '{trackingNumber}'.");
        }

        var now = DateTimeOffset.UtcNow;

        // 4. Create DeliveryConfirmation entity
        var confirmation = new DeliveryConfirmation
        {
            ParcelId = parcel.Id,
            ReceivedBy = request.ReceivedBy,
            DeliveryLocation = request.DeliveryLocation,
            SignatureImage = request.SignatureImage,
            DeliveredAt = request.DeliveredAt,
            CreatedAt = now
        };
        await _confirmationRepository.AddAsync(confirmation, ct);

        // 5. Create TrackingEvent with EventType.Delivered
        var trackingEvent = new TrackingEvent
        {
            ParcelId = parcel.Id,
            Timestamp = request.DeliveredAt,
            EventType = EventType.Delivered,
            Description = $"Delivered to {request.ReceivedBy} at {request.DeliveryLocation}",
            LocationCity = parcel.RecipientAddress?.City,
            LocationState = parcel.RecipientAddress?.State,
            LocationCountry = parcel.RecipientAddress?.CountryCode
        };
        await _parcelRepository.AddTrackingEventAsync(trackingEvent, ct);

        // 6. Update parcel status and delivery date
        parcel.Status = ParcelStatus.Delivered;
        parcel.ActualDeliveryDate = request.DeliveredAt;
        parcel.UpdatedAt = now;

        // 7. Save all changes in a single transaction
        await _parcelRepository.SaveChangesAsync(ct);

        // 8. Return response
        return new DeliveryConfirmationResponse
        {
            Id = confirmation.Id,
            TrackingNumber = parcel.TrackingNumber,
            ReceivedBy = confirmation.ReceivedBy,
            DeliveryLocation = confirmation.DeliveryLocation,
            HasSignature = !string.IsNullOrEmpty(confirmation.SignatureImage),
            DeliveredAt = confirmation.DeliveredAt,
            CreatedAt = confirmation.CreatedAt
        };
    }

    /// <inheritdoc/>
    public async Task<DeliveryConfirmationDetailResponse?> GetDeliveryConfirmationAsync(
        string trackingNumber,
        CancellationToken ct = default)
    {
        // 1. Load parcel by tracking number
        var parcel = await _parcelRepository.GetByTrackingNumberWithRecipientAsync(trackingNumber, ct);
        if (parcel is null)
        {
            return null;
        }

        // 2. Load delivery confirmation
        var confirmation = await _confirmationRepository.GetByParcelIdAsync(parcel.Id, ct);
        if (confirmation is null)
        {
            return null;
        }

        // 3. Calculate isOnTime
        var isOnTime = CalculateIsOnTime(confirmation.DeliveredAt, parcel.EstimatedDeliveryDate);

        // 4. Return full detail response
        return new DeliveryConfirmationDetailResponse
        {
            Id = confirmation.Id,
            TrackingNumber = parcel.TrackingNumber,
            ReceivedBy = confirmation.ReceivedBy,
            DeliveryLocation = confirmation.DeliveryLocation,
            SignatureImage = confirmation.SignatureImage,
            DeliveredAt = confirmation.DeliveredAt,
            EstimatedDeliveryDate = parcel.EstimatedDeliveryDate,
            IsOnTime = isOnTime,
            CreatedAt = confirmation.CreatedAt
        };
    }

    /// <summary>
    /// Calculates whether the delivery was on time.
    /// On-time = deliveredAt date is on or before estimatedDeliveryDate date.
    /// If no estimate exists, returns false.
    /// </summary>
    public static bool CalculateIsOnTime(DateTimeOffset deliveredAt, DateTimeOffset? estimatedDeliveryDate)
    {
        if (!estimatedDeliveryDate.HasValue)
            return false;

        return deliveredAt.Date <= estimatedDeliveryDate.Value.Date;
    }
}
