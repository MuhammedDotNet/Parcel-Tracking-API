namespace ParcelTracking.Application.DTOs;

public record DeliveryEstimateResponse
{
    public DateOnly EarliestDelivery { get; init; }
    public DateOnly LatestDelivery { get; init; }
    public string Confidence { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;
    public bool IsInternational { get; init; }
    public string? DeliveryTimeZoneId { get; init; }
}
