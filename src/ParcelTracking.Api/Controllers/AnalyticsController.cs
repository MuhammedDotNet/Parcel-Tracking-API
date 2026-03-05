using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.Interfaces;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

/// <summary>
/// Analytics endpoints for aggregated parcel statistics and insights
/// </summary>
[ApiController]
[ApiVersion("1.0")]

[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[Tags("Analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    /// <summary>
    /// Get delivery performance statistics for a date range
    /// </summary>
    /// <param name="from">Start date (defaults to 30 days ago)</param>
    /// <param name="to">End date (defaults to now)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Delivery statistics including counts, averages, and on-time percentage</returns>
    [HttpGet("delivery-stats")]
    [ResponseCache(CacheProfileName = "AnalyticsShort")]
    [ProducesResponseType(typeof(Application.DTOs.DeliveryStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryStats(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        // Default to last 30 days if parameters are null
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var result = await _analytics.GetDeliveryStatsAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get top exception reasons with counts and percentages
    /// </summary>
    /// <param name="from">Start date (defaults to 30 days ago)</param>
    /// <param name="to">End date (defaults to now)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of exception reasons ordered by count descending</returns>
    [HttpGet("exception-reasons")]
    [ResponseCache(CacheProfileName = "Analytics")]
    [ProducesResponseType(typeof(List<Application.DTOs.ExceptionReasonResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExceptionReasons(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        // Default to last 30 days if parameters are null
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var result = await _analytics.GetTopExceptionReasonsAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get parcel count and average delivery time per service type
    /// </summary>
    /// <param name="from">Start date (defaults to 30 days ago)</param>
    /// <param name="to">End date (defaults to now)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Service breakdown with counts and average delivery times</returns>
    [HttpGet("service-breakdown")]
    [ResponseCache(CacheProfileName = "Analytics")]
    [ProducesResponseType(typeof(List<Application.DTOs.ServiceBreakdownResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceBreakdown(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        // Default to last 30 days if parameters are null
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var result = await _analytics.GetServiceBreakdownAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get current count of parcels in each status
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Pipeline status with parcel counts for each status</returns>
    [HttpGet("pipeline")]
    [ResponseCache(CacheProfileName = "RealTime")]
    [ProducesResponseType(typeof(List<Application.DTOs.PipelineStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPipeline(CancellationToken ct)
    {
        var result = await _analytics.GetPipelineAsync(ct);
        return Ok(result);
    }
}
