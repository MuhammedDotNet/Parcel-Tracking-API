namespace ParcelTracking.Application.DTOs;

public record WeightDto
{
    public decimal Value { get; init; }
    public string Unit { get; init; } = string.Empty; // "kg" | "lb"
}
