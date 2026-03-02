namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Parcel count for a specific status
/// </summary>
public record PipelineStatusResponse
{
    /// <summary>
    /// Parcel status name
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Number of parcels in this status
    /// </summary>
    public int Count { get; init; }
}
