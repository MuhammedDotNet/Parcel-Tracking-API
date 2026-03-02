namespace ParcelTracking.Application.DTOs;

/// <summary>
/// Parcel dimensions with unit
/// </summary>
public record DimensionsDto
{
    /// <summary>
    /// Length dimension
    /// </summary>
    /// <example>30.5</example>
    public decimal Length { get; init; }
    
    /// <summary>
    /// Width dimension
    /// </summary>
    /// <example>20.0</example>
    public decimal Width { get; init; }
    
    /// <summary>
    /// Height dimension
    /// </summary>
    /// <example>15.5</example>
    public decimal Height { get; init; }
    
    /// <summary>
    /// Dimension unit (cm or in)
    /// </summary>
    /// <example>cm</example>
    public string Unit { get; init; } = string.Empty;
}
