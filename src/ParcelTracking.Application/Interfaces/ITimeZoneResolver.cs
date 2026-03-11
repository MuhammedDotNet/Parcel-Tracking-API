using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

/// <summary>
/// Resolves IANA timezone ID from an address.
/// </summary>
public interface ITimeZoneResolver
{
    string GetIanaTimeZone(Address address);
}
