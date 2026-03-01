using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
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
    private readonly IParcelStatusService _statusService;
    private readonly IParcelService _parcelService;

    public ParcelsController(
        IParcelRegistrationService registrationService,
        IParcelRetrievalService retrievalService,
        IParcelStatusService statusService,
        IParcelService parcelService)
    {
        _registrationService = registrationService;
        _retrievalService = retrievalService;
        _statusService = statusService;
        _parcelService = parcelService;
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

    /// <summary>Search and filter parcels with cursor-based pagination.</summary>
    [HttpGet]
    [ResponseCache(Duration = 30, VaryByQueryKeys = ["*"])]
    [ProducesResponseType(typeof(PagedResult<ParcelSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] ParcelSearchParams searchParams,
        CancellationToken ct)
    {
        // Validate date range
        var dateValidation = ParcelSearchParams.ValidateDateRange(
            searchParams.CreatedFrom,
            searchParams.CreatedTo);

        if (!dateValidation.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "One or more validation errors occurred.",
                Detail = dateValidation.ErrorMessage,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }

        // Clamp page size to valid range
        searchParams.PageSize = ParcelSearchParams.ClampPageSize(searchParams.PageSize);

        // Execute search
        var result = await _parcelService.SearchParcelsAsync(searchParams, ct);

        return Ok(result);
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

    /// <summary>
    /// Partially update a parcel using JSON Patch (RFC 6902).
    /// </summary>
    /// <param name="id">The internal parcel ID.</param>
    /// <param name="patchDoc">JSON Patch document containing operations to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated parcel if successful.</returns>
    /// <remarks>
    /// This endpoint allows partial updates to parcels using JSON Patch operations.
    /// 
    /// **Whitelisted Fields (can be modified):**
    /// - `status` - Parcel status (validated by state machine)
    /// - `serviceType` - Service type (Standard, Express, Overnight, Economy)
    /// - `description` - Parcel description (max 500 characters)
    /// - `estimatedDeliveryDate` - Estimated delivery date
    /// 
    /// **Read-only Fields (cannot be modified):**
    /// - `id`, `trackingNumber`, `createdAt`, `shipperAddressId`, `recipientAddressId`
    /// 
    /// **Status Transition Rules:**
    /// - LabelCreated → PickedUp, Exception
    /// - PickedUp → InTransit, Exception
    /// - InTransit → OutForDelivery, Exception
    /// - OutForDelivery → Delivered, Exception
    /// - Exception → Returned
    /// - Delivered → (terminal, no transitions)
    /// - Returned → (terminal, no transitions)
    /// 
    /// **Terminal State Protection:**
    /// Parcels in terminal states (Delivered, Returned) cannot be modified.
    /// 
    /// **Example Request:**
    /// ```json
    /// [
    ///   { "op": "replace", "path": "/description", "value": "Updated description" },
    ///   { "op": "replace", "path": "/serviceType", "value": "Express" }
    /// ]
    /// ```
    /// 
    /// **Example Success Response (200 OK):**
    /// ```json
    /// {
    ///   "id": 123,
    ///   "trackingNumber": "PKG-ABC123",
    ///   "status": "PickedUp",
    ///   "serviceType": "Express",
    ///   "description": "Updated description",
    ///   ...
    /// }
    /// ```
    /// 
    /// **Example Error Response - Invalid Transition (422 Unprocessable Entity):**
    /// ```json
    /// {
    ///   "error": "invalid_transition",
    ///   "message": "Cannot transition from 'LabelCreated' to 'Delivered'",
    ///   "currentStatus": "LabelCreated",
    ///   "requestedStatus": "Delivered",
    ///   "allowedStatuses": ["PickedUp", "Exception"]
    /// }
    /// ```
    /// 
    /// **Example Error Response - Terminal State (422 Unprocessable Entity):**
    /// ```json
    /// {
    ///   "error": "terminal_state",
    ///   "message": "Parcel is in terminal state 'Delivered' and cannot be modified",
    ///   "currentStatus": "Delivered"
    /// }
    /// ```
    /// 
    /// **Example Error Response - Invalid Path (422 Unprocessable Entity):**
    /// ```json
    /// {
    ///   "ParcelPatchModel": [
    ///     "The target location specified by path '/trackingNumber' was not found."
    ///   ]
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Parcel updated successfully.</response>
    /// <response code="404">Parcel not found.</response>
    /// <response code="422">Business rule violation (invalid transition, terminal state, or invalid patch operation).</response>
    [HttpPatch("{id:int}")]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType(typeof(ParcelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Patch(
        int id,
        [FromBody] JsonPatchDocument<ParcelPatchModel> patchDoc,
        CancellationToken ct)
    {
        // 1. Get writable parcel (throws if terminal)
        var parcel = await _parcelService.GetWritableParcelAsync(id, ct);
        if (parcel is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Parcel Not Found",
                Detail = $"No parcel exists with ID '{id}'.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        // 2. Map to patch model
        var patchModel = new ParcelPatchModel
        {
            Status = parcel.Status,
            ServiceType = parcel.ServiceType,
            Description = parcel.Description,
            EstimatedDeliveryDate = parcel.EstimatedDeliveryDate
        };

        // 3. Apply patch
        patchDoc.ApplyTo(patchModel, ModelState);
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        // 4. Handle status change if present
        if (patchModel.Status != parcel.Status)
        {
            var result = await _statusService.TransitionStatusAsync(id, patchModel.Status, ct);
            return ToActionResult(result);
        }

        // 5. Apply other changes
        var updated = await _parcelService.UpdateParcelDetailsAsync(id, patchModel, ct);
        return Ok(MapToResponse(updated));
    }

    private IActionResult ToActionResult(StatusTransitionResult result)
    {
        if (result.IsSuccess)
        {
            return Ok(MapToResponse(result.Parcel!));
        }

        return result.ErrorType switch
        {
            "not_found" => NotFound(new
            {
                error = result.ErrorType,
                message = result.ErrorMessage,
                parcelId = result.ParcelId
            }),

            "terminal_state" => UnprocessableEntity(new
            {
                error = result.ErrorType,
                message = result.ErrorMessage,
                currentStatus = result.CurrentStatus?.ToString()
            }),

            "invalid_transition" => UnprocessableEntity(new
            {
                error = result.ErrorType,
                message = result.ErrorMessage,
                currentStatus = result.CurrentStatus?.ToString(),
                requestedStatus = result.RequestedStatus?.ToString(),
                allowedStatuses = result.AllowedStatuses?.Select(s => s.ToString())
            }),

            _ => StatusCode(500, new { error = "internal_error", message = "An unexpected error occurred" })
        };
    }

    private static ParcelResponse MapToResponse(Domain.Entities.Parcel p) => new()
    {
        Id = p.Id,
        TrackingNumber = p.TrackingNumber,
        ShipperAddressId = p.ShipperAddressId,
        RecipientAddressId = p.RecipientAddressId,
        ServiceType = p.ServiceType.ToString(),
        Status = p.Status.ToString(),
        Description = p.Description ?? string.Empty,
        Weight = new WeightDto { Value = p.Weight, Unit = p.WeightUnit.ToString() },
        Dimensions = new DimensionsDto
        {
            Length = p.Length,
            Width = p.Width,
            Height = p.Height,
            Unit = p.DimensionUnit.ToString()
        },
        DeclaredValue = new DeclaredValueDto { Amount = p.DeclaredValue, Currency = p.Currency },
        ContentItems = p.ContentItems.Select(ci => new ContentItemDto
        {
            HsCode = ci.HsCode,
            Description = ci.Description,
            Quantity = ci.Quantity,
            UnitValue = ci.UnitValue,
            Currency = ci.Currency,
            Weight = ci.Weight,
            WeightUnit = ci.WeightUnit.ToString(),
            CountryOfOrigin = ci.CountryOfOrigin
        }).ToList(),
        EstimatedDeliveryDate = p.EstimatedDeliveryDate,
        CreatedAt = p.CreatedAt
    };
}
