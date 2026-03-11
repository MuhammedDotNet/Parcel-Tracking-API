namespace ParcelTracking.Application.DTOs;

public record ParcelDetailResponse
{
    public int Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public string WeightUnit { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public AddressResponse ShipperAddress { get; init; } = null!;
    public AddressResponse RecipientAddress { get; init; } = null!;
    public List<ContentItemResponse> ContentItems { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public int DaysInTransit { get; init; }
    public bool IsDelivered { get; init; }
}
