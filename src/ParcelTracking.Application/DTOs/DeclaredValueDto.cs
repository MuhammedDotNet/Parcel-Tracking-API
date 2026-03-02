namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Declared value for insurance purposes
/// </summary>
public record DeclaredValueDto
{
    /// <summary>
    /// Monetary amount
    /// </summary>
    /// <example>500.00</example>
    public decimal Amount { get; init; }
    
    /// <summary>
    /// ISO 4217 currency code
    /// </summary>
    /// <example>USD</example>
    public string Currency { get; init; } = string.Empty;
}
