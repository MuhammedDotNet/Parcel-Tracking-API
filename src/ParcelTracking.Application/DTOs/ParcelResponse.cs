namespace ParcelTracking.Application.DTOs;

public record ParcelResponse
{
    public int Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public int ShipperAddressId { get; init; }
    public int RecipientAddressId { get; init; }
    public string ServiceType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public WeightDto Weight { get; init; } = null!;
    public DimensionsDto Dimensions { get; init; } = null!;
    public DeclaredValueDto DeclaredValue { get; init; } = null!;
    public List<ContentItemDto> ContentItems { get; init; } = new();
    public DateTimeOffset? EstimatedDeliveryDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
