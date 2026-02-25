namespace ParcelTracking.Application.DTOs;

public record DeclaredValueDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty; // ISO 4217
}
