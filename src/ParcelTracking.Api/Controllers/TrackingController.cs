using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/[controller]")]
[Route("api/v{version:apiVersion}/[controller]")]
[Tags("Tracking")]
public class TrackingController : ControllerBase
{
    private readonly IParcelRetrievalService _retrievalService;

    public TrackingController(IParcelRetrievalService retrievalService)
        => _retrievalService = retrievalService;

    /// <summary>Get public tracking information by tracking number.</summary>
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType(typeof(TrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTrackingNumber(
        string trackingNumber, CancellationToken ct)
    {
        var response = await _retrievalService.GetByTrackingNumberAsync(trackingNumber, ct);

        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Tracking Number Not Found",
                Detail = $"No parcel found with tracking number '{trackingNumber}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(response);
    }
}
