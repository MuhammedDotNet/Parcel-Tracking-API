namespace ParcelTracking.Application.DTOs;

public record CreateAddressRequest
{
    public string Street1 { get; init; } = string.Empty;
    public string? Street2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public bool IsResidential { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string? Email { get; init; }
}
