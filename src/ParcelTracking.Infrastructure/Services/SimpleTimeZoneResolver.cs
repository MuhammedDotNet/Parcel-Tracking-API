using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Infrastructure.Services;

/// <summary>
/// Resolves IANA timezone ID from an address using a two-tier lookup:
/// US state → country default → "Etc/UTC" fallback.
/// </summary>
public sealed class SimpleTimeZoneResolver : ITimeZoneResolver
{
    // US: state-level resolution
    private static readonly Dictionary<string, string> UsStateTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IL"] = "America/Chicago",
        ["NY"] = "America/New_York",
        ["CA"] = "America/Los_Angeles",
        ["TX"] = "America/Chicago",
        ["CO"] = "America/Denver",
        ["WA"] = "America/Los_Angeles",
        ["FL"] = "America/New_York",
    };

    // Country-level defaults (capital / most populous timezone)
    private static readonly Dictionary<string, string> CountryTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = "America/New_York",
        ["GB"] = "Europe/London",
        ["DE"] = "Europe/Berlin",
        ["FR"] = "Europe/Paris",
        ["JP"] = "Asia/Tokyo",
        ["CN"] = "Asia/Shanghai",
        ["AU"] = "Australia/Sydney",
        ["CA"] = "America/Toronto",
        ["MX"] = "America/Mexico_City",
        ["IN"] = "Asia/Kolkata",
    };

    public string GetIanaTimeZone(Address address)
    {
        // US addresses: try state-level first
        if (string.Equals(address.CountryCode, "US", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(address.State)
            && UsStateTimeZones.TryGetValue(address.State, out var stateZone))
        {
            return stateZone;
        }

        // All countries: fall back to country default
        if (!string.IsNullOrEmpty(address.CountryCode)
            && CountryTimeZones.TryGetValue(address.CountryCode, out var countryZone))
        {
            return countryZone;
        }

        return "Etc/UTC"; // safe fallback
    }
}
