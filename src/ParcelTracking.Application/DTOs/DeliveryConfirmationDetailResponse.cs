namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Full GET response for delivery confirmation retrieval.
/// Includes the full signatureImage and calculated isOnTime flag.
/// </summary>
public record DeliveryConfirmationDetailResponse
{
    /// <summary>Unique identifier of the delivery confirmation.</summary>
    public int Id { get; init; }

    /// <summary>Tracking number of the parcel.</summary>
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Name of the person who received the parcel.</summary>
    public string ReceivedBy { get; init; } = string.Empty;

    /// <summary>Location where the parcel was delivered.</summary>
    public string DeliveryLocation { get; init; } = string.Empty;

    /// <summary>Full base64-encoded signature image, if provided.</summary>
    public string? SignatureImage { get; init; }

    /// <summary>Timestamp when the parcel was delivered.</summary>
    public DateTimeOffset DeliveredAt { get; init; }

    /// <summary>Estimated delivery date of the parcel, if available.</summary>
    public DateTimeOffset? EstimatedDeliveryDate { get; init; }

    /// <summary>Whether the parcel was delivered on or before the estimated delivery date.</summary>
    public bool IsOnTime { get; init; }

    /// <summary>Timestamp when the confirmation record was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
