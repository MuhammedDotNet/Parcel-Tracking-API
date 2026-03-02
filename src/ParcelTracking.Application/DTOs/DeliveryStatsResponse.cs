namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Delivery performance statistics for a date range
/// </summary>
public record DeliveryStatsResponse
{
    /// <summary>
    /// Start of the date range
    /// </summary>
    public DateTimeOffset From { get; init; }

    /// <summary>
    /// End of the date range
    /// </summary>
    public DateTimeOffset To { get; init; }

    /// <summary>
    /// Total number of parcels created in the date range
    /// </summary>
    public int TotalParcels { get; init; }

    /// <summary>
    /// Number of parcels with Delivered status
    /// </summary>
    public int Delivered { get; init; }

    /// <summary>
    /// Number of parcels in transit (InTransit, OutForDelivery, PickedUp)
    /// </summary>
    public int InTransit { get; init; }

    /// <summary>
    /// Number of parcels with Exception status
    /// </summary>
    public int Exceptions { get; init; }

    /// <summary>
    /// Average delivery time in hours for delivered parcels
    /// </summary>
    public double AverageDeliveryTimeHours { get; init; }

    /// <summary>
    /// Percentage of parcels delivered on or before estimated date
    /// </summary>
    public double OnTimePercentage { get; init; }
}
