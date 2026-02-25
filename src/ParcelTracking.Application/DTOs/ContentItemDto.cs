namespace ParcelTracking.Application.DTOs;

public record ContentItemDto
{
    public string HsCode { get; init; } = string.Empty; // XXXX.XX
    public string Description { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitValue { get; init; }
    public string Currency { get; init; } = "USD";        // ISO 4217
    public decimal Weight { get; init; }
    public string WeightUnit { get; init; } = string.Empty; // "kg" | "lb"
    public string CountryOfOrigin { get; init; } = string.Empty; // ISO 3166-1 alpha-2
}
