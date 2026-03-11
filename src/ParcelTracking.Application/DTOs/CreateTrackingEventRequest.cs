using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.DTOs;

public record CreateTrackingEventRequest
{
    public DateTimeOffset Timestamp { get; init; }
    public EventType EventType { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? LocationCity { get; init; }
    public string? LocationState { get; init; }
    public string? LocationCountry { get; init; }
    public string? DelayReason { get; init; }
}
