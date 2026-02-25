using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;

namespace ParcelTracking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ParcelsController : ControllerBase
{
    private readonly IParcelRegistrationService _service;

    public ParcelsController(IParcelRegistrationService service)
        => _service = service;

    /// <summary>Register a new parcel and generate a tracking number.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ParcelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Register(
        RegisterParcelRequest request,
        CancellationToken ct)
    {
        try
        {
            var response = await _service.RegisterAsync(request, ct);

            return CreatedAtAction(
                nameof(GetByTrackingNumber),
                new { trackingNumber = response.TrackingNumber },
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Resource not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>Get a parcel by tracking number. (Placeholder — implemented in a future lesson.)</summary>
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType(typeof(ParcelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByTrackingNumber(string trackingNumber)
        => NotFound();
}
