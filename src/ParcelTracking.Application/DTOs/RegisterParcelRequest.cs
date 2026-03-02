namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Request to register a new parcel in the system
/// </summary>
public record RegisterParcelRequest
{
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
    /// Service type for delivery (Standard, Express, Overnight, Economy)
    /// </summary>
    /// <example>Express</example>
    public string ServiceType { get; init; } = string.Empty;
    
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
    /// Declared value for insurance purposes
    /// </summary>
    public DeclaredValueDto DeclaredValue { get; init; } = null!;
    
    /// <summary>
    /// List of items contained in the parcel
    /// </summary>
    public List<ContentItemDto> ContentItems { get; init; } = new();
}
