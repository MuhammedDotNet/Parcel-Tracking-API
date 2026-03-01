using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.DTOs;

public class ParcelSearchParams
{
    public ParcelStatus? Status { get; set; }
    public ServiceType? ServiceType { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public int PageSize { get; set; } = 20;
    public string? Cursor { get; set; }

    /// <summary>
    /// Clamps the page size to the valid range of 1-100.
    /// </summary>
    /// <param name="requestedPageSize">The requested page size</param>
    /// <returns>A page size between 1 and 100 inclusive</returns>
    public static int ClampPageSize(int requestedPageSize)
    {
        if (requestedPageSize < 1)
        {
            return 1;
        }
        
        if (requestedPageSize > 100)
        {
            return 100;
        }
        
        return requestedPageSize;
    }

    /// <summary>
    /// Validates that CreatedFrom is before or equal to CreatedTo.
    /// </summary>
    /// <param name="createdFrom">The start date of the range</param>
    /// <param name="createdTo">The end date of the range</param>
    /// <returns>A validation result indicating success or failure</returns>
    public static ValidationResult ValidateDateRange(DateTimeOffset? createdFrom, DateTimeOffset? createdTo)
    {
        // If either or both are null, the range is valid (open-ended)
        if (!createdFrom.HasValue || !createdTo.HasValue)
        {
            return ValidationResult.Success();
        }

        // If CreatedFrom is after CreatedTo, the range is invalid
        if (createdFrom.Value > createdTo.Value)
        {
            return ValidationResult.Failure("CreatedFrom must be before or equal to CreatedTo");
        }

        return ValidationResult.Success();
    }
}
