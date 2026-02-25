using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Mappings;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Services;

public sealed class ParcelRetrievalService : IParcelRetrievalService
{
    private readonly IParcelRepository _repository;

    public ParcelRetrievalService(IParcelRepository repository)
        => _repository = repository;

    public async Task<ParcelDetailResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        var parcel = await _repository.GetByIdWithDetailsAsync(id, ct);
        return parcel is null ? null : MapToDetailResponse(parcel);
    }

    public async Task<TrackingResponse?> GetByTrackingNumberAsync(
        string trackingNumber, CancellationToken ct)
    {
        var parcel = await _repository.GetByTrackingNumberWithRecipientAsync(trackingNumber, ct);
        return parcel is null ? null : MapToTrackingResponse(parcel);
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static ParcelDetailResponse MapToDetailResponse(Parcel parcel) => new()
    {
        Id = parcel.Id,
        TrackingNumber = parcel.TrackingNumber,
        Status = parcel.Status.ToString(),
        Weight = parcel.Weight,
        WeightUnit = parcel.WeightUnit.ToString(),
        Description = parcel.Description ?? string.Empty,
        ShipperAddress = parcel.ShipperAddress.ToResponse(),
        RecipientAddress = parcel.RecipientAddress.ToResponse(),
        ContentItems = parcel.ContentItems.Select(MapToContentItemResponse).ToList(),
        CreatedAt = parcel.CreatedAt,
        DeliveredAt = parcel.ActualDeliveryDate,
        DaysInTransit = CalculateDaysInTransit(parcel),
        IsDelivered = parcel.Status == ParcelStatus.Delivered
    };

    private static TrackingResponse MapToTrackingResponse(Parcel parcel) => new()
    {
        TrackingNumber = parcel.TrackingNumber,
        Status = parcel.Status.ToString(),
        RecipientCity = parcel.RecipientAddress.City,
        RecipientState = parcel.RecipientAddress.State,
        Weight = parcel.Weight,
        ShippedAt = parcel.CreatedAt,
        DeliveredAt = parcel.ActualDeliveryDate,
        DaysInTransit = CalculateDaysInTransit(parcel),
        IsDelivered = parcel.Status == ParcelStatus.Delivered
    };

    private static ContentItemResponse MapToContentItemResponse(ParcelContentItem ci) => new()
    {
        HsCode = ci.HsCode,
        Description = ci.Description,
        Quantity = ci.Quantity,
        UnitValue = ci.UnitValue,
        Currency = ci.Currency,
        Weight = ci.Weight,
        WeightUnit = ci.WeightUnit.ToString(),
        CountryOfOrigin = ci.CountryOfOrigin
    };

    // ── Calculated fields ──────────────────────────────────────────────────

    private static int CalculateDaysInTransit(Parcel parcel)
    {
        var endDate = parcel.ActualDeliveryDate ?? DateTimeOffset.UtcNow;
        return (int)(endDate - parcel.CreatedAt).TotalDays;
    }
}
