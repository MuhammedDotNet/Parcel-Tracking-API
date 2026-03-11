using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/parcels/{parcelId}/events")]
[Tags("Tracking Events")]
public class TrackingEventsController : ControllerBase
{
    private readonly ITrackingService _trackingService;

    public TrackingEventsController(ITrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    /// <summary>Create a new tracking event for a parcel.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TrackingEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateEvent(
        int parcelId,
        CreateTrackingEventRequest request,
        CancellationToken ct)
    {
        try
        {
            var response = await _trackingService.AddEventAsync(parcelId, request, ct);

            return CreatedAtAction(
                nameof(GetHistory),
                new { parcelId = response.ParcelId },
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>Get tracking event history for a parcel.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrackingEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        int parcelId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        try
        {
            var events = await _trackingService.GetHistoryAsync(parcelId, from, to, ct);
            return Ok(events);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid date range",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }
    }
}
