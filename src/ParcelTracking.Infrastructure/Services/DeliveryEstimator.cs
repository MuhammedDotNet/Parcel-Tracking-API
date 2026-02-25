using ParcelTracking.Application.Interfaces;

namespace ParcelTracking.Infrastructure.Services;

/// <summary>
/// Calculates estimated delivery dates by adding business days (Mon–Fri) based on service type.
/// Weekends are skipped; e.g. Overnight registered on Friday delivers Monday.
/// </summary>
public sealed class DeliveryEstimator : IDeliveryEstimator
{
    private static readonly Dictionary<string, int> TransitDays =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Economy"] = 10,
            ["Standard"] = 7,
            ["Express"] = 3,
            ["Overnight"] = 1,
        };

    public DateTimeOffset Estimate(string serviceType, DateTimeOffset registrationDate)
    {
        var days = TransitDays.GetValueOrDefault(serviceType, 7);
        return AddBusinessDays(registrationDate, days);
    }

    private static DateTimeOffset AddBusinessDays(DateTimeOffset start, int days)
    {
        var current = start;
        var added = 0;

        while (added < days)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                added++;
        }

        return current;
    }
}
