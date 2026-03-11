namespace ParcelTracking.Application.DTOs;

public record ParcelSearchResponse
{
    public int Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;
    public string ShipperCity { get; init; } = string.Empty;
    public string RecipientCity { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? EstimatedDeliveryDate { get; init; }
}
