using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.DTOs;

public class StatusTransitionResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorType { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int? ParcelId { get; private init; }
    public ParcelStatus? CurrentStatus { get; private init; }
    public ParcelStatus? RequestedStatus { get; private init; }
    public IReadOnlySet<ParcelStatus>? AllowedStatuses { get; private init; }
    public Parcel? Parcel { get; private init; }

    public static StatusTransitionResult Success(Parcel parcel)
    {
        return new StatusTransitionResult
        {
            IsSuccess = true,
            Parcel = parcel
        };
    }

    public static StatusTransitionResult NotFound(int parcelId)
    {
        return new StatusTransitionResult
        {
            IsSuccess = false,
            ErrorType = "not_found",
            ErrorMessage = $"Parcel with ID {parcelId} was not found",
            ParcelId = parcelId
        };
    }

    public static StatusTransitionResult TerminalState(ParcelStatus current)
    {
        return new StatusTransitionResult
        {
            IsSuccess = false,
            ErrorType = "terminal_state",
            ErrorMessage = $"Parcel is in terminal state '{current}' and cannot be modified",
            CurrentStatus = current
        };
    }

    public static StatusTransitionResult InvalidTransition(
        ParcelStatus current,
        ParcelStatus requested,
        IReadOnlySet<ParcelStatus> allowed)
    {
        return new StatusTransitionResult
        {
            IsSuccess = false,
            ErrorType = "invalid_transition",
            ErrorMessage = $"Cannot transition from '{current}' to '{requested}'",
            CurrentStatus = current,
            RequestedStatus = requested,
            AllowedStatuses = allowed
        };
    }
}
