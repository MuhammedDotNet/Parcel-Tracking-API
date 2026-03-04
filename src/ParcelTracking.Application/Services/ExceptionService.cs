using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.Services;

public class ExceptionService : IExceptionService
{
    private readonly IParcelRepository _repository;

    private const int MaxDeliveryAttempts = 3;

    private static readonly ParcelStatus[] ExceptionEligibleStatuses =
    {
        ParcelStatus.InTransit,
        ParcelStatus.OutForDelivery
    };

    public ExceptionService(IParcelRepository repository)
    {
        _repository = repository;
    }

    public async Task<Parcel> ReportExceptionAsync(
        int parcelId,
        ReportExceptionRequest request,
        CancellationToken ct = default)
    {
        var parcel = await _repository.GetByIdWithRecipientAsync(parcelId, ct);

        if (parcel is null)
            throw new KeyNotFoundException("Parcel not found");

        // Validate transition through state machine
        if (!ParcelStatusRules.CanTransition(parcel.Status, ParcelStatus.Exception))
            throw new InvalidOperationException(
                $"Cannot report exception for parcel in {parcel.Status} status");

        // Transition to Exception status
        parcel.Status = ParcelStatus.Exception;
        parcel.DeliveryAttempts += 1;
        parcel.UpdatedAt = DateTimeOffset.UtcNow;

        // Create tracking event
        var trackingEvent = new TrackingEvent
        {
            ParcelId = parcelId,
            Timestamp = DateTimeOffset.UtcNow,
            EventType = EventType.DeliveryAttempted,
            Description = $"Delivery exception reported: {request.Reason}",
            DelayReason = request.Reason.ToString(),
            LocationCity = parcel.RecipientAddress?.City,
            LocationState = parcel.RecipientAddress?.State,
            LocationCountry = parcel.RecipientAddress?.CountryCode
        };

        await _repository.AddTrackingEventAsync(trackingEvent, ct);
        await _repository.SaveChangesAsync(ct);

        return parcel;
    }

    public async Task<(Parcel Parcel, bool AutoReturned)> RetryDeliveryAsync(
        int parcelId,
        RetryDeliveryRequest request,
        CancellationToken ct = default)
    {
        var parcel = await _repository.GetByIdWithRecipientAsync(parcelId, ct);

        if (parcel is null)
            throw new KeyNotFoundException("Parcel not found");

        if (parcel.Status != ParcelStatus.Exception)
            throw new InvalidOperationException(
                $"Cannot retry delivery for parcel in {parcel.Status} status. Only parcels in Exception status can be retried.");

        if (request.NewEstimatedDeliveryDate <= DateTimeOffset.UtcNow)
            throw new ArgumentException("New estimated delivery date must be in the future");

        // Check max delivery attempts - auto-return
        if (parcel.DeliveryAttempts >= MaxDeliveryAttempts)
        {
            parcel.Status = ParcelStatus.Returned;
            parcel.UpdatedAt = DateTimeOffset.UtcNow;

            var returnEvent = new TrackingEvent
            {
                ParcelId = parcelId,
                Timestamp = DateTimeOffset.UtcNow,
                EventType = EventType.Returned,
                Description = $"Parcel automatically returned to sender. Maximum delivery attempts ({MaxDeliveryAttempts}) reached.",
                LocationCity = parcel.RecipientAddress?.City,
                LocationState = parcel.RecipientAddress?.State,
                LocationCountry = parcel.RecipientAddress?.CountryCode
            };

            await _repository.AddTrackingEventAsync(returnEvent, ct);
            await _repository.SaveChangesAsync(ct);

            return (parcel, true);
        }

        // Under max attempts - retry (validated by state machine: Exception → InTransit)
        parcel.Status = ParcelStatus.InTransit;
        parcel.EstimatedDeliveryDate = request.NewEstimatedDeliveryDate;
        parcel.UpdatedAt = DateTimeOffset.UtcNow;

        var trackingEvent = new TrackingEvent
        {
            ParcelId = parcelId,
            Timestamp = DateTimeOffset.UtcNow,
            EventType = EventType.InTransit,
            Description = $"Redelivery scheduled. New estimated delivery date: {request.NewEstimatedDeliveryDate:yyyy-MM-dd}",
            LocationCity = parcel.RecipientAddress?.City,
            LocationState = parcel.RecipientAddress?.State,
            LocationCountry = parcel.RecipientAddress?.CountryCode
        };

        await _repository.AddTrackingEventAsync(trackingEvent, ct);
        await _repository.SaveChangesAsync(ct);

        return (parcel, false);
    }

    public async Task<List<Parcel>> GetExceptionParcelsAsync(CancellationToken ct = default)
    {
        return await _repository.GetExceptionParcelsAsync(ct);
    }
}
