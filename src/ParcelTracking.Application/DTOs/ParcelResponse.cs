namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Response containing parcel details
/// </summary>
public record ParcelResponse
{
    /// <summary>
    /// Internal parcel ID
    /// </summary>
    /// <example>123</example>
    public int Id { get; init; }
    
    /// <summary>
    /// Unique tracking number for the parcel
    /// </summary>
    /// <example>PKG-ABC123XYZ</example>
    public string TrackingNumber { get; init; } = string.Empty;
    
    /// <summary>
    /// ID of the shipper address
    /// </summary>
    /// <example>1</example>
    public int ShipperAddressId { get; init; }
    
    /// <summary>
    /// ID of the recipient address
    /// </summary>
    /// <example>2</example>
    public int RecipientAddressId { get; init; }
    
    /// <summary>
    /// Service type for delivery
    /// </summary>
    /// <example>Express</example>
    public string ServiceType { get; init; } = string.Empty;
    
    /// <summary>
    /// Current status of the parcel
    /// </summary>
    /// <example>InTransit</example>
    public string Status { get; init; } = string.Empty;
    
    /// <summary>
    /// Description of the parcel contents
    /// </summary>
    /// <example>Electronics and accessories</example>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Weight of the parcel
    /// </summary>
    public WeightDto Weight { get; init; } = null!;
    
    /// <summary>
    /// Dimensions of the parcel
    /// </summary>
    public DimensionsDto Dimensions { get; init; } = null!;
    
    /// <summary>
    /// Declared value for insurance
    /// </summary>
    public DeclaredValueDto DeclaredValue { get; init; } = null!;
    
    /// <summary>
    /// List of items in the parcel
    /// </summary>
    public List<ContentItemDto> ContentItems { get; init; } = new();
    
    /// <summary>
    /// Estimated delivery date
    /// </summary>
    /// <example>2026-03-10T00:00:00Z</example>
    public DateTimeOffset? EstimatedDeliveryDate { get; init; }
    
    /// <summary>
    /// Timestamp when the parcel was created
    /// </summary>
    /// <example>2026-03-02T10:30:00Z</example>
    public DateTimeOffset CreatedAt { get; init; }
}
