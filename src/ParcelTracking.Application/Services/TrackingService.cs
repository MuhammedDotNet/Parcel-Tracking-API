using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Services;

public class TrackingService : ITrackingService
{
    private readonly IParcelRepository _repository;

    public TrackingService(IParcelRepository repository)
    {
        _repository = repository;
    }

    public async Task<TrackingEventResponse> AddEventAsync(
        int parcelId, 
        CreateTrackingEventRequest request, 
        CancellationToken ct)
    {
        // Check if parcel exists
        var parcelExists = await _repository.ParcelExistsAsync(parcelId, ct);
        if (!parcelExists)
        {
            throw new KeyNotFoundException($"Parcel {parcelId} not found.");
        }

        // Query for the most recent event for the parcel
        var latestEvent = await _repository.GetLatestTrackingEventAsync(parcelId, ct);

        // Validate chronological ordering (if there are existing events)
        if (latestEvent != null && request.Timestamp < latestEvent.Timestamp)
        {
            throw new InvalidOperationException(
                $"Event timestamp {request.Timestamp} is earlier than the most recent event at {latestEvent.Timestamp}.");
        }

        // Create event entity from request
        var trackingEvent = new TrackingEvent
        {
            ParcelId = parcelId,
            Timestamp = request.Timestamp,
            EventType = request.EventType,
            Description = request.Description,
            LocationCity = request.LocationCity,
            LocationState = request.LocationState,
            LocationCountry = request.LocationCountry,
            DelayReason = request.DelayReason
        };

        // Add event to DbContext
        await _repository.AddTrackingEventAsync(trackingEvent, ct);

        // Map EventType to ParcelStatus using switch expression
        var newStatus = MapEventTypeToStatus(request.EventType);

        // Update parcel status
        var parcel = await _repository.GetByIdAsync(parcelId, ct);
        if (parcel != null)
        {
            parcel.Status = newStatus;
        }

        // Save changes atomically
        await _repository.SaveChangesAsync(ct);

        // Map to response
        return new TrackingEventResponse
        {
            Id = trackingEvent.Id,
            ParcelId = trackingEvent.ParcelId,
            Timestamp = trackingEvent.Timestamp,
            EventType = trackingEvent.EventType.ToString(),
            Description = trackingEvent.Description,
            LocationCity = trackingEvent.LocationCity,
            LocationState = trackingEvent.LocationState,
            LocationCountry = trackingEvent.LocationCountry,
            DelayReason = trackingEvent.DelayReason
        };
    }

    private static ParcelStatus MapEventTypeToStatus(EventType eventType)
    {
        return eventType switch
        {
            EventType.PickedUp => ParcelStatus.PickedUp,
            EventType.DepartedFacility => ParcelStatus.InTransit,
            EventType.ArrivedAtFacility => ParcelStatus.InTransit,
            EventType.InTransit => ParcelStatus.InTransit,
            EventType.OutForDelivery => ParcelStatus.OutForDelivery,
            EventType.DeliveryAttempted => ParcelStatus.OutForDelivery,
            EventType.Delivered => ParcelStatus.Delivered,
            EventType.Exception => ParcelStatus.Exception,
            EventType.Returned => ParcelStatus.Returned,
            _ => ParcelStatus.LabelCreated // Default for other types
        };
    }

    public async Task<IEnumerable<TrackingEventResponse>> GetHistoryAsync(
        int parcelId, 
        DateTimeOffset? from, 
        DateTimeOffset? to, 
        CancellationToken ct)
    {
        // Check if parcel exists
        var parcelExists = await _repository.ParcelExistsAsync(parcelId, ct);
        if (!parcelExists)
        {
            throw new KeyNotFoundException($"Parcel {parcelId} not found.");
        }

        // Validate date range (from <= to)
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("'from' date must be earlier than or equal to 'to' date.");
        }

        // Build query with conditional date filters and order by Timestamp ascending
        var events = await _repository.GetTrackingEventsAsync(parcelId, from, to, ct);

        // Project to TrackingEventResponse DTOs
        return events.Select(e => new TrackingEventResponse
        {
            Id = e.Id,
            ParcelId = e.ParcelId,
            Timestamp = e.Timestamp,
            EventType = e.EventType.ToString(),
            Description = e.Description,
            LocationCity = e.LocationCity,
            LocationState = e.LocationState,
            LocationCountry = e.LocationCountry,
            DelayReason = e.DelayReason
        });
    }
}
