namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Request to create a new address
/// </summary>
public record CreateAddressRequest
{
    /// <summary>
    /// Primary street address
    /// </summary>
    /// <example>123 Main Street</example>
    public string Street1 { get; init; } = string.Empty;
    
    /// <summary>
    /// Secondary street address (apartment, suite, etc.)
    /// </summary>
    /// <example>Apt 4B</example>
    public string? Street2 { get; init; }
    
    /// <summary>
    /// City name
    /// </summary>
    /// <example>New York</example>
    public string City { get; init; } = string.Empty;
    
    /// <summary>
    /// State or province code
    /// </summary>
    /// <example>NY</example>
    public string State { get; init; } = string.Empty;
    
    /// <summary>
    /// Postal or ZIP code
    /// </summary>
    /// <example>10001</example>
    public string PostalCode { get; init; } = string.Empty;
    
    /// <summary>
    /// ISO 3166-1 alpha-2 country code
    /// </summary>
    /// <example>US</example>
    public string CountryCode { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether this is a residential address
    /// </summary>
    /// <example>true</example>
    public bool IsResidential { get; init; }
    
    /// <summary>
    /// Contact person name
    /// </summary>
    /// <example>John Doe</example>
    public string ContactName { get; init; } = string.Empty;
    
    /// <summary>
    /// Company name (for business addresses)
    /// </summary>
    /// <example>Acme Corp</example>
    public string? CompanyName { get; init; }
    
    /// <summary>
    /// Contact phone number
    /// </summary>
    /// <example>+1-555-0123</example>
    public string Phone { get; init; } = string.Empty;
    
    /// <summary>
    /// Contact email address
    /// </summary>
    /// <example>john.doe@example.com</example>
    public string? Email { get; init; }
}
