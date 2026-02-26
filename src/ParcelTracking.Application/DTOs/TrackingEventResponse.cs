namespace ParcelTracking.Application.DTOs;

public record TrackingEventResponse
{
    public int Id { get; init; }
    public int ParcelId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LocationCity { get; init; }
    public string? LocationState { get; init; }
    public string? LocationCountry { get; init; }
    public string? DelayReason { get; init; }
}
