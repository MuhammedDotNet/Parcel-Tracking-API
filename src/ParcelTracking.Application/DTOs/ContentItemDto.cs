namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Individual item contained in a parcel
/// </summary>
public record ContentItemDto
{
    /// <summary>
    /// Harmonized System code for customs (format: XXXX.XX)
    /// </summary>
    /// <example>8517.12</example>
    public string HsCode { get; init; } = string.Empty;
    
    /// <summary>
    /// Description of the item
    /// </summary>
    /// <example>Smartphone</example>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Quantity of this item
    /// </summary>
    /// <example>2</example>
    public int Quantity { get; init; }
    
    /// <summary>
    /// Value per unit
    /// </summary>
    /// <example>299.99</example>
    public decimal UnitValue { get; init; }
    
    /// <summary>
    /// ISO 4217 currency code
    /// </summary>
    /// <example>USD</example>
    public string Currency { get; init; } = "USD";
    
    /// <summary>
    /// Weight of the item
    /// </summary>
    /// <example>0.5</example>
    public decimal Weight { get; init; }
    
    /// <summary>
    /// Weight unit (kg or lb)
    /// </summary>
    /// <example>kg</example>
    public string WeightUnit { get; init; } = string.Empty;
    
    /// <summary>
    /// ISO 3166-1 alpha-2 country code of origin
    /// </summary>
    /// <example>CN</example>
    public string CountryOfOrigin { get; init; } = string.Empty;
}
