using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Services;

public sealed class ParcelRegistrationService : IParcelRegistrationService
{
    private readonly IParcelRepository _repository;
    private readonly ITrackingNumberGenerator _trackingNumberGenerator;
    private readonly IDeliveryEstimator _deliveryEstimator;

    public ParcelRegistrationService(
        IParcelRepository repository,
        ITrackingNumberGenerator trackingNumberGenerator,
        IDeliveryEstimator deliveryEstimator)
    {
        _repository = repository;
        _trackingNumberGenerator = trackingNumberGenerator;
        _deliveryEstimator = deliveryEstimator;
    }

    public async Task<ParcelResponse> RegisterAsync(
        RegisterParcelRequest request,
        CancellationToken ct)
    {
        // Step 1: Verify referenced addresses exist (early-fail before any work)
        if (!await _repository.AddressExistsAsync(request.ShipperAddressId, ct))
            throw new KeyNotFoundException(
                $"Shipper address {request.ShipperAddressId} not found.");

        if (!await _repository.AddressExistsAsync(request.RecipientAddressId, ct))
            throw new KeyNotFoundException(
                $"Recipient address {request.RecipientAddressId} not found.");

        // Step 2: Build the Parcel entity
        var now = DateTimeOffset.UtcNow;
        var trackingNumber = _trackingNumberGenerator.Generate();

        var parcel = new Parcel
        {
            TrackingNumber = trackingNumber,
            ShipperAddressId = request.ShipperAddressId,
            RecipientAddressId = request.RecipientAddressId,
            ServiceType = Enum.Parse<ServiceType>(request.ServiceType, ignoreCase: true),
            Status = ParcelStatus.LabelCreated,
            Description = request.Description,
            Weight = request.Weight.Value,
            WeightUnit = Enum.Parse<WeightUnit>(request.Weight.Unit, ignoreCase: true),
            Length = request.Dimensions.Length,
            Width = request.Dimensions.Width,
            Height = request.Dimensions.Height,
            DimensionUnit = Enum.Parse<DimensionUnit>(request.Dimensions.Unit, ignoreCase: true),
            DeclaredValue = request.DeclaredValue.Amount,
            Currency = request.DeclaredValue.Currency,
            EstimatedDeliveryDate = _deliveryEstimator.Estimate(request.ServiceType, now),
            CreatedAt = now,
            UpdatedAt = now,
            ContentItems = request.ContentItems.Select(ci => new ParcelContentItem
            {
                HsCode = ci.HsCode,
                Description = ci.Description,
                Quantity = ci.Quantity,
                UnitValue = ci.UnitValue,
                Currency = ci.Currency,
                Weight = ci.Weight,
                WeightUnit = Enum.Parse<WeightUnit>(ci.WeightUnit, ignoreCase: true),
                CountryOfOrigin = ci.CountryOfOrigin
            }).ToList()
        };

        // Step 3: Create the initial tracking event
        var initialEvent = new TrackingEvent
        {
            Parcel = parcel,
            EventType = EventType.LabelCreated,
            Description = "Label created, shipment information sent to carrier",
            Timestamp = now
        };

        // Step 4: Persist both atomically in one SaveChangesAsync call
        await _repository.AddAsync(parcel, ct);
        await _repository.AddTrackingEventAsync(initialEvent, ct);
        await _repository.SaveChangesAsync(ct);

        return MapToResponse(parcel);
    }

    private static ParcelResponse MapToResponse(Parcel p) => new()
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
