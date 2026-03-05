using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using Asp.Versioning;

namespace ParcelTracking.Api.Controllers;

/// <summary>
/// Manages address records for shippers and recipients
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
[Tags("Addresses")]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _service;

    public AddressesController(IAddressService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get all addresses in the system
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all addresses</returns>
    /// <response code="200">Addresses retrieved successfully</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AddressResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AddressResponse>>> GetAll(CancellationToken ct)
    {
        var addresses = await _service.GetAllAsync(ct);
        return Ok(addresses);
    }

    /// <summary>
    /// Get a specific address by ID
    /// </summary>
    /// <param name="id">The address ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Address details if found</returns>
    /// <response code="200">Address retrieved successfully</response>
    /// <response code="404">Address not found</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressResponse>> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Create a new address
    /// </summary>
    /// <param name="request">Address creation data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created address with assigned ID</returns>
    /// <response code="201">Address created successfully</response>
    /// <response code="400">Invalid input data</response>
    [HttpPost]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AddressResponse>> Create(CreateAddressRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update an existing address
    /// </summary>
    /// <param name="id">The address ID</param>
    /// <param name="request">Updated address data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated address details</returns>
    /// <response code="200">Address updated successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="404">Address not found</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddressResponse>> Update(int id, UpdateAddressRequest request, CancellationToken ct)
    {
        var updated = await _service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Delete an address
    /// </summary>
    /// <param name="id">The address ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Address deleted successfully</response>
    /// <response code="404">Address not found</response>
    /// <response code="409">Address is in use by parcels and cannot be deleted</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.NotFound) return NotFound();
        if (result.Conflict)
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Conflict",
                Detail = result.ConflictMessage
            });
        return NoContent();
    }
}
