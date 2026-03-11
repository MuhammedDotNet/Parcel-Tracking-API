namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Request body for confirming delivery of a parcel.
/// </summary>
public record ConfirmDeliveryRequest
{
    /// <summary>Name of the person who received the parcel.</summary>
    public required string ReceivedBy { get; init; }

    /// <summary>Location where the parcel was delivered.</summary>
    public required string DeliveryLocation { get; init; }

    /// <summary>Optional base64-encoded signature image.</summary>
    public string? SignatureImage { get; init; }

    /// <summary>Timestamp when the parcel was delivered.</summary>
    public required DateTimeOffset DeliveredAt { get; init; }
}
