using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.Services;

public class ParcelStatusService : IParcelStatusService
{
    private readonly IParcelRepository _repository;

    public ParcelStatusService(IParcelRepository repository)
    {
        _repository = repository;
    }

    public async Task<StatusTransitionResult> TransitionStatusAsync(
        int parcelId,
        ParcelStatus newStatus,
        CancellationToken ct = default)
    {
        // 1. Lookup parcel
        var parcel = await _repository.GetByIdAsync(parcelId, ct);

        if (parcel is null)
        {
            return StatusTransitionResult.NotFound(parcelId);
        }

        // 2. Check if current status is terminal
        if (ParcelStatusRules.IsTerminal(parcel.Status))
        {
            return StatusTransitionResult.TerminalState(parcel.Status);
        }

        // 3. Validate transition
        if (!ParcelStatusRules.CanTransition(parcel.Status, newStatus))
        {
            var allowedStatuses = ParcelStatusRules.GetAllowedTransitions(parcel.Status);
            return StatusTransitionResult.InvalidTransition(parcel.Status, newStatus, allowedStatuses);
        }

        // 4. Update status
        parcel.Status = newStatus;
        parcel.UpdatedAt = DateTimeOffset.UtcNow;

        // 5. Create tracking event
        var trackingEvent = new TrackingEvent
        {
            ParcelId = parcelId,
            Timestamp = DateTimeOffset.UtcNow,
            EventType = MapStatusToEventType(newStatus),
            Description = $"Status changed to {newStatus}"
        };

        await _repository.AddTrackingEventAsync(trackingEvent, ct);

        // 6. Save changes
        await _repository.SaveChangesAsync(ct);

        return StatusTransitionResult.Success(parcel);
    }

    private static EventType MapStatusToEventType(ParcelStatus status)
    {
        return status switch
        {
            ParcelStatus.LabelCreated => EventType.LabelCreated,
            ParcelStatus.PickedUp => EventType.PickedUp,
            ParcelStatus.InTransit => EventType.InTransit,
            ParcelStatus.OutForDelivery => EventType.OutForDelivery,
            ParcelStatus.Delivered => EventType.Delivered,
            ParcelStatus.Exception => EventType.Exception,
            ParcelStatus.Returned => EventType.Returned,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown parcel status")
        };
    }
}
