using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Mappings;

public static class AddressMappingExtensions
{
    public static AddressResponse ToResponse(this Address address)
    {
        return new AddressResponse
        {
            Id = address.Id,
            Street1 = address.Street1,
            Street2 = address.Street2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode,
            IsResidential = address.IsResidential,
            ContactName = address.ContactName,
            CompanyName = address.CompanyName,
            Phone = address.Phone,
            Email = address.Email
        };
    }

    public static Address ToEntity(this CreateAddressRequest request)
    {
        return new Address
        {
            Street1 = request.Street1,
            Street2 = request.Street2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            CountryCode = request.CountryCode,
            IsResidential = request.IsResidential,
            ContactName = request.ContactName,
            CompanyName = request.CompanyName,
            Phone = request.Phone,
            Email = request.Email
        };
    }

    public static void ApplyUpdate(this Address entity, UpdateAddressRequest request)
    {
        entity.Street1 = request.Street1;
        entity.Street2 = request.Street2;
        entity.City = request.City;
        entity.State = request.State;
        entity.PostalCode = request.PostalCode;
        entity.CountryCode = request.CountryCode;
        entity.IsResidential = request.IsResidential;
        entity.ContactName = request.ContactName;
        entity.CompanyName = request.CompanyName;
        entity.Phone = request.Phone;
        entity.Email = request.Email;
    }
}
