namespace ParcelTracking.Application.DTOs;

public record TrackingResponse
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RecipientCity { get; init; } = string.Empty;
    public string RecipientState { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public DateTimeOffset ShippedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public int DaysInTransit { get; init; }
    public bool IsDelivered { get; init; }
}
