using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

public interface IAnalyticsService
{
    /// <summary>
    /// Gets delivery performance statistics for a date range
    /// </summary>
    /// <param name="from">Start of the date range</param>
    /// <param name="to">End of the date range</param>
    /// <returns>Delivery statistics including counts, averages, and percentages</returns>
    Task<DeliveryStatsResponse> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Gets top exception reasons with counts and percentages
    /// </summary>
    /// <param name="from">Start of the date range</param>
    /// <param name="to">End of the date range</param>
    /// <returns>List of exception reasons ordered by count descending</returns>
    Task<List<ExceptionReasonResponse>> GetTopExceptionReasonsAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Gets parcel count and average delivery time per service type
    /// </summary>
    /// <param name="from">Start of the date range</param>
    /// <param name="to">End of the date range</param>
    /// <returns>List of service type statistics</returns>
    Task<List<ServiceBreakdownResponse>> GetServiceBreakdownAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Gets current count of parcels in each status
    /// </summary>
    /// <returns>List of status counts including all ParcelStatus enum values</returns>
    Task<List<PipelineStatusResponse>> GetPipelineAsync();
}
