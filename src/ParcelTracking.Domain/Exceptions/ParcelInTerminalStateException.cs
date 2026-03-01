using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Domain.Exceptions;

public class ParcelInTerminalStateException : Exception
{
    public int ParcelId { get; }
    public ParcelStatus Status { get; }

    public ParcelInTerminalStateException(int parcelId, ParcelStatus status)
        : base($"Parcel {parcelId} is in terminal state '{status}' and cannot be modified")
    {
        ParcelId = parcelId;
        Status = status;
    }
}
