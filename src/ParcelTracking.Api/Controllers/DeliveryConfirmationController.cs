using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

/// <summary>
/// Handles delivery confirmation endpoints for parcels.
/// POST: Create a delivery confirmation for a parcel.
/// GET: Retrieve delivery confirmation details for a parcel.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/parcels/{trackingNumber}/delivery-confirmation")]
[Route("api/v{version:apiVersion}/parcels/{trackingNumber}/delivery-confirmation")]
[Tags("Delivery Confirmation")]
public class DeliveryConfirmationController : ControllerBase
{
    private readonly IDeliveryConfirmationService _service;

    public DeliveryConfirmationController(IDeliveryConfirmationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Confirm delivery of a parcel with proof-of-delivery data.
    /// </summary>
    /// <param name="trackingNumber">The parcel tracking number.</param>
    /// <param name="request">Delivery confirmation data including receiver, location, and optional signature.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with confirmation details and Location header.</returns>
    /// <response code="201">Delivery confirmation created successfully.</response>
    /// <response code="400">Invalid input data or parcel status.</response>
    /// <response code="404">Parcel not found.</response>
    /// <response code="409">Delivery confirmation already exists for this parcel.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DeliveryConfirmationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmDelivery(
        string trackingNumber,
        [FromBody] ConfirmDeliveryRequest request,
        CancellationToken ct)
    {
        try
        {
            var response = await _service.ConfirmDeliveryAsync(trackingNumber, request, ct);

            return CreatedAtAction(
                nameof(GetDeliveryConfirmation),
                new { trackingNumber },
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
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFLICT:"))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate delivery confirmation",
                Detail = ex.Message["CONFLICT:".Length..],
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext.Request.Path,
                Extensions = { ["trackingNumber"] = trackingNumber }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid parcel status",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// Retrieve delivery confirmation details for a parcel.
    /// </summary>
    /// <param name="trackingNumber">The parcel tracking number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with full delivery confirmation details.</returns>
    /// <response code="200">Delivery confirmation details retrieved successfully.</response>
    /// <response code="404">Parcel or delivery confirmation not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(DeliveryConfirmationDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeliveryConfirmation(
        string trackingNumber,
        CancellationToken ct)
    {
        var result = await _service.GetDeliveryConfirmationAsync(trackingNumber, ct);
        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Delivery confirmation not found",
                Detail = $"No delivery confirmation found for parcel '{trackingNumber}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result);
    }
}
