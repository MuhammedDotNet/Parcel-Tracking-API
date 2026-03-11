namespace ParcelTracking.Application.DTOs;

public class DeliveryEstimateResult
{
    public DateOnly EarliestDelivery { get; init; }
    public DateOnly LatestDelivery { get; init; }
    public DeliveryConfidenceLevel Confidence { get; init; }
    public string? DeliveryTimeZoneId { get; init; }
}

public enum DeliveryConfidenceLevel
{
    Low,
    Medium,
    High
}

public record TransitTimeRange(int MinDays, int MaxDays);
