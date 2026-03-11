namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Public tracking information for a parcel
/// </summary>
public record TrackingResponse
{
    /// <summary>
    /// Unique tracking number
    /// </summary>
    /// <example>PKG-ABC123XYZ</example>
    public string TrackingNumber { get; init; } = string.Empty;
    
    /// <summary>
    /// Current parcel status
    /// </summary>
    /// <example>InTransit</example>
    public string Status { get; init; } = string.Empty;
    
    /// <summary>
    /// Recipient city
    /// </summary>
    /// <example>New York</example>
    public string RecipientCity { get; init; } = string.Empty;
    
    /// <summary>
    /// Recipient state
    /// </summary>
    /// <example>NY</example>
    public string RecipientState { get; init; } = string.Empty;
    
    /// <summary>
    /// Parcel weight in the specified unit
    /// </summary>
    /// <example>2.5</example>
    public decimal Weight { get; init; }
    
    /// <summary>
    /// Timestamp when the parcel was shipped
    /// </summary>
    /// <example>2026-03-02T10:30:00Z</example>
    public DateTimeOffset ShippedAt { get; init; }
    
    /// <summary>
    /// Timestamp when the parcel was delivered (null if not delivered)
    /// </summary>
    /// <example>2026-03-05T14:20:00Z</example>
    public DateTimeOffset? DeliveredAt { get; init; }
    
    /// <summary>
    /// Number of days the parcel has been in transit
    /// </summary>
    /// <example>3</example>
    public int DaysInTransit { get; init; }
    
    /// <summary>
    /// Whether the parcel has been delivered
    /// </summary>
    /// <example>true</example>
    public bool IsDelivered { get; init; }
}
