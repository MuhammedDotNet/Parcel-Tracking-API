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
    private readonly IParcelRegistrationService _registrationService;
    private readonly IParcelRetrievalService _retrievalService;

    public ParcelsController(
        IParcelRegistrationService registrationService,
        IParcelRetrievalService retrievalService)
    {
        _registrationService = registrationService;
        _retrievalService = retrievalService;
    }

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
            var response = await _registrationService.RegisterAsync(request, ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = response.Id },
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

    /// <summary>Get full parcel details by internal ID (internal use).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ParcelDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var response = await _retrievalService.GetByIdAsync(id, ct);

        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel Not Found",
                Detail = $"No parcel exists with ID '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(response);
    }
}
