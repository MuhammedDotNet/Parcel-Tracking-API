using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Services;

public class DeliveryEstimationService : IDeliveryEstimationService
{
    private readonly ITimeZoneResolver _timeZoneResolver;

    public DeliveryEstimationService(ITimeZoneResolver timeZoneResolver)
    {
        _timeZoneResolver = timeZoneResolver;
    }

    private static readonly Dictionary<ServiceType, TransitTimeRange> DomesticTransitTimes = new()
    {
        [ServiceType.Economy] = new TransitTimeRange(5, 7),
        [ServiceType.Standard] = new TransitTimeRange(3, 5),
        [ServiceType.Express] = new TransitTimeRange(1, 2),
        [ServiceType.Overnight] = new TransitTimeRange(1, 1)
    };

    private const int InternationalMinAdditionalDays = 3;
    private const int InternationalMaxAdditionalDays = 5;

    public DeliveryEstimateResult Calculate(Parcel parcel)
    {
        var isInternational = IsInternational(parcel);
        var transitRange = GetTransitTimeRange(parcel.ServiceType, isInternational);
        var startDate = DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime);
        var tzId = _timeZoneResolver.GetIanaTimeZone(parcel.RecipientAddress);

        return new DeliveryEstimateResult
        {
            EarliestDelivery = AddBusinessDays(startDate, transitRange.MinDays),
            LatestDelivery = AddBusinessDays(startDate, transitRange.MaxDays),
            Confidence = DetermineConfidence(parcel.Status),
            DeliveryTimeZoneId = tzId
        };
    }

    public DeliveryEstimateResult Recalculate(Parcel parcel, DateOnly fromDate)
    {
        var isInternational = IsInternational(parcel);
        var transitRange = GetTransitTimeRange(parcel.ServiceType, isInternational);

        var elapsed = CountBusinessDays(
            DateOnly.FromDateTime(parcel.CreatedAt.UtcDateTime), fromDate);
        var remainingMin = Math.Max(1, transitRange.MinDays - elapsed);
        var remainingMax = Math.Max(1, transitRange.MaxDays - elapsed);
        var tzId = _timeZoneResolver.GetIanaTimeZone(parcel.RecipientAddress);

        return new DeliveryEstimateResult
        {
            EarliestDelivery = AddBusinessDays(fromDate, remainingMin),
            LatestDelivery = AddBusinessDays(fromDate, remainingMax),
            Confidence = DetermineConfidence(parcel.Status),
            DeliveryTimeZoneId = tzId
        };
    }

    public static bool IsInternational(Parcel parcel)
    {
        return !string.Equals(
            parcel.ShipperAddress.CountryCode,
            parcel.RecipientAddress.CountryCode,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static TransitTimeRange GetTransitTimeRange(ServiceType serviceType, bool isInternational)
    {
        var baseRange = DomesticTransitTimes[serviceType];

        if (isInternational)
        {
            return new TransitTimeRange(
                baseRange.MinDays + InternationalMinAdditionalDays,
                baseRange.MaxDays + InternationalMaxAdditionalDays);
        }

        return baseRange;
    }

    internal static DateOnly AddBusinessDays(DateOnly startDate, int businessDays)
    {
        var current = startDate;
        var added = 0;

        while (added < businessDays)
        {
            current = current.AddDays(1);

            if (current.DayOfWeek is not DayOfWeek.Saturday
                and not DayOfWeek.Sunday)
            {
                added++;
            }
        }

        return current;
    }

    internal static int CountBusinessDays(DateOnly startDate, DateOnly endDate)
    {
        var count = 0;
        var current = startDate;

        while (current < endDate)
        {
            current = current.AddDays(1);

            if (current.DayOfWeek is not DayOfWeek.Saturday
                and not DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    internal static DeliveryConfidenceLevel DetermineConfidence(ParcelStatus status)
    {
        return status switch
        {
            ParcelStatus.OutForDelivery => DeliveryConfidenceLevel.High,
            ParcelStatus.Delivered => DeliveryConfidenceLevel.High,
            ParcelStatus.InTransit => DeliveryConfidenceLevel.Medium,
            _ => DeliveryConfidenceLevel.Low
        };
    }
}
