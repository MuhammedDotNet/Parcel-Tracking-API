namespace ParcelTracking.Domain.Exceptions;

/// <summary>
/// Thrown when a delivery confirmation already exists for a parcel.
/// </summary>
public class DuplicateDeliveryConfirmationException : Exception
{
    public string TrackingNumber { get; }

    public DuplicateDeliveryConfirmationException(string trackingNumber)
        : base($"Delivery confirmation already exists for parcel '{trackingNumber}'.")
    {
        TrackingNumber = trackingNumber;
    }
}
