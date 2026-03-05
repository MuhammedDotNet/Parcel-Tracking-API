using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Mappings;

/// <summary>
/// Extension methods for mapping Parcel entities to response DTOs.
/// Centralizes all Parcel mapping logic to avoid duplication.
/// </summary>
public static class ParcelMappingExtensions
{
    /// <summary>
    /// Maps a Parcel entity to a ParcelResponse DTO.
    /// </summary>
    public static ParcelResponse ToResponse(this Parcel p) => new()
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
        DeliveryTimeZoneId = p.DeliveryTimeZoneId,
        CreatedAt = p.CreatedAt
    };

    /// <summary>
    /// Maps a Parcel entity (with loaded addresses) to a ParcelDetailResponse DTO.
    /// </summary>
    public static ParcelDetailResponse ToDetailResponse(this Parcel p) => new()
    {
        Id = p.Id,
        TrackingNumber = p.TrackingNumber,
        Status = p.Status.ToString(),
        Weight = p.Weight,
        WeightUnit = p.WeightUnit.ToString(),
        Description = p.Description ?? string.Empty,
        ShipperAddress = p.ShipperAddress?.ToResponse()!,
        RecipientAddress = p.RecipientAddress?.ToResponse()!,
        CreatedAt = p.CreatedAt,
        DeliveredAt = p.ActualDeliveryDate,
        DaysInTransit = (int)(DateTimeOffset.UtcNow - p.CreatedAt).TotalDays,
        IsDelivered = p.Status == ParcelStatus.Delivered
    };
}
