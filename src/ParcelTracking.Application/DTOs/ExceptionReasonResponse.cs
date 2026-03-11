namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Exception reason with count and percentage
/// </summary>
public record ExceptionReasonResponse
{
    /// <summary>
    /// The exception/delay reason
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Number of exceptions with this reason
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Percentage of total exceptions
    /// </summary>
    public double Percentage { get; init; }
}
