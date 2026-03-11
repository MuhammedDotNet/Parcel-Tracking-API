namespace ParcelTracking.Application.DTOs;

public sealed class DeleteResult
{
    public bool NotFound { get; init; }
    public bool Conflict { get; init; }
    public string? ConflictMessage { get; init; }
}
