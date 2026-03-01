using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Helpers;

/// <summary>
/// Provides validation methods for search parameters.
/// </summary>
public static class SearchParamsValidator
{
    /// <summary>
    /// Validates and normalizes search parameters.
    /// </summary>
    /// <param name="searchParams">The search parameters to validate</param>
    /// <returns>A validation result with normalized parameters or error message</returns>
    public static (bool IsValid, string? ErrorMessage, ParcelSearchParams NormalizedParams) Validate(
        ParcelSearchParams searchParams)
    {
        // Create a copy with normalized values
        var normalized = new ParcelSearchParams
        {
            Status = searchParams.Status,
            ServiceType = searchParams.ServiceType,
            CreatedFrom = searchParams.CreatedFrom,
            CreatedTo = searchParams.CreatedTo,
            City = searchParams.City,
            Country = searchParams.Country,
            Keyword = searchParams.Keyword,
            SortBy = searchParams.SortBy,
            SortDescending = searchParams.SortDescending,
            PageSize = ParcelSearchParams.ClampPageSize(searchParams.PageSize),
            Cursor = searchParams.Cursor
        };

        // Validate date range
        var dateRangeResult = ParcelSearchParams.ValidateDateRange(
            searchParams.CreatedFrom,
            searchParams.CreatedTo);

        if (!dateRangeResult.IsValid)
        {
            return (false, dateRangeResult.ErrorMessage, normalized);
        }

        return (true, null, normalized);
    }
}
