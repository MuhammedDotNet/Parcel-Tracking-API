using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a parcel in an invalid status.
/// </summary>
public class InvalidParcelStatusException : Exception
{
    public ParcelStatus CurrentStatus { get; }
    public ParcelStatus[] ValidStatuses { get; }

    public InvalidParcelStatusException(ParcelStatus currentStatus, params ParcelStatus[] validStatuses)
        : base($"Parcel status '{currentStatus}' is not valid for this operation. " +
               $"Valid statuses: {string.Join(", ", validStatuses)}.")
    {
        CurrentStatus = currentStatus;
        ValidStatuses = validStatuses;
    }
}
