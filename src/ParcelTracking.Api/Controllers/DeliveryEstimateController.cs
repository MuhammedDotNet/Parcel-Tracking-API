using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Services;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

/// <summary>
/// Provides delivery estimate calculations for parcels.
/// GET: Retrieve a delivery estimate for a parcel.
/// PUT: Recalculate a delivery estimate after exceptions or delays.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/parcels/{id:int}/delivery-estimate")]
[Route("api/v{version:apiVersion}/parcels/{id:int}/delivery-estimate")]
[Tags("Delivery Estimation")]
public class DeliveryEstimateController : ControllerBase
{
    private readonly IParcelRepository _repository;
    private readonly IDeliveryEstimationService _estimationService;

    public DeliveryEstimateController(
        IParcelRepository repository,
        IDeliveryEstimationService estimationService)
    {
        _repository = repository;
        _estimationService = estimationService;
    }

    /// <summary>
    /// Get the delivery estimate for a parcel.
    /// </summary>
    /// <param name="id">The parcel ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with delivery estimate details.</returns>
    /// <response code="200">Delivery estimate retrieved successfully.</response>
    /// <response code="404">Parcel not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(DeliveryEstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEstimate(int id, CancellationToken ct)
    {
        var parcel = await _repository.GetByIdWithAddressesAsync(id, ct);

        if (parcel is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel not found",
                Detail = $"No parcel exists with ID '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        var result = _estimationService.Calculate(parcel);
        var response = MapToResponse(result, parcel);

        return Ok(response);
    }

    /// <summary>
    /// Recalculate the delivery estimate for a parcel after exceptions or delays.
    /// </summary>
    /// <param name="id">The parcel ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the updated delivery estimate.</returns>
    /// <response code="200">Delivery estimate recalculated successfully.</response>
    /// <response code="404">Parcel not found.</response>
    [HttpPut("recalculate")]
    [ProducesResponseType(typeof(DeliveryEstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recalculate(int id, CancellationToken ct)
    {
        var parcel = await _repository.GetByIdWithAddressesAndEventsAsync(id, ct);

        if (parcel is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel not found",
                Detail = $"No parcel exists with ID '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        // Use the most recent tracking event timestamp, or fall back to current date
        var latestEvent = parcel.TrackingEvents.FirstOrDefault();
        var fromDate = latestEvent is not null
            ? DateOnly.FromDateTime(latestEvent.Timestamp.UtcDateTime)
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var result = _estimationService.Recalculate(parcel, fromDate);

        // Persist the updated estimate
        parcel.EstimatedDeliveryDate = new DateTimeOffset(
            result.LatestDelivery.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        parcel.DeliveryTimeZoneId = result.DeliveryTimeZoneId;
        parcel.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);

        var response = MapToResponse(result, parcel);
        return Ok(response);
    }

    private static DeliveryEstimateResponse MapToResponse(
        DeliveryEstimateResult result,
        Domain.Entities.Parcel parcel)
    {
        return new DeliveryEstimateResponse
        {
            EarliestDelivery = result.EarliestDelivery,
            LatestDelivery = result.LatestDelivery,
            Confidence = result.Confidence.ToString(),
            ServiceType = parcel.ServiceType.ToString(),
            IsInternational = DeliveryEstimationService.IsInternational(parcel),
            DeliveryTimeZoneId = result.DeliveryTimeZoneId
        };
    }
}

