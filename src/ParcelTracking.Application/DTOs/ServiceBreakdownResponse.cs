namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Service type statistics
/// </summary>
public record ServiceBreakdownResponse
{
    /// <summary>
    /// Service type name (Economy, Standard, Express, Overnight)
    /// </summary>
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>
    /// Number of parcels with this service type
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Average delivery time in hours for this service type
    /// </summary>
    public double AverageDeliveryTimeHours { get; init; }
}
