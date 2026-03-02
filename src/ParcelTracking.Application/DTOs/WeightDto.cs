namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Weight measurement with unit
/// </summary>
public record WeightDto
{
    /// <summary>
    /// Weight value
    /// </summary>
    /// <example>2.5</example>
    public decimal Value { get; init; }
    
    /// <summary>
    /// Weight unit (kg or lb)
    /// </summary>
    /// <example>kg</example>
    public string Unit { get; init; } = string.Empty;
}
