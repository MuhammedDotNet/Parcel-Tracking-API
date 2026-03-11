namespace ParcelTracking.Application.DTOs;

public record RetryDeliveryRequest
{
    public DateTimeOffset NewEstimatedDeliveryDate { get; init; }
}
