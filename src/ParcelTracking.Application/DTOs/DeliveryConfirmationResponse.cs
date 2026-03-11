namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Lightweight POST response for delivery confirmation creation.
/// Contains hasSignature flag instead of full signatureImage.
/// </summary>
public record DeliveryConfirmationResponse
{
    /// <summary>Unique identifier of the delivery confirmation.</summary>
    public int Id { get; init; }

    /// <summary>Tracking number of the parcel.</summary>
    public string TrackingNumber { get; init; } = string.Empty;

    /// <summary>Name of the person who received the parcel.</summary>
    public string ReceivedBy { get; init; } = string.Empty;

    /// <summary>Location where the parcel was delivered.</summary>
    public string DeliveryLocation { get; init; } = string.Empty;

    /// <summary>Whether a signature image was provided.</summary>
    public bool HasSignature { get; init; }

    /// <summary>Timestamp when the parcel was delivered.</summary>
    public DateTimeOffset DeliveredAt { get; init; }

    /// <summary>Timestamp when the confirmation record was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
