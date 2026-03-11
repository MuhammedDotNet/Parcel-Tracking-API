namespace ParcelTracking.Application.DTOs;

public record ContentItemResponse
{
    public string HsCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitValue { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public string WeightUnit { get; init; } = string.Empty;
    public string CountryOfOrigin { get; init; } = string.Empty;
}
