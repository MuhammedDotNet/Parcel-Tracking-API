using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Helpers;

public static class ParcelQueryBuilder
{
    /// <summary>
    /// Applies dynamic filters to a parcel query based on the provided search parameters.
    /// Filters are conditionally applied only when the corresponding parameter is provided.
    /// </summary>
    /// <param name="query">The base IQueryable to filter</param>
    /// <param name="searchParams">The search parameters containing filter criteria</param>
    /// <returns>A filtered IQueryable</returns>
    public static IQueryable<Parcel> ApplyFilters(
        IQueryable<Parcel> query,
        ParcelSearchParams searchParams)
    {
        // Status filter
        if (searchParams.Status.HasValue)
        {
            query = query.Where(p => p.Status == searchParams.Status.Value);
        }

        // Service type filter
        if (searchParams.ServiceType.HasValue)
        {
            query = query.Where(p => p.ServiceType == searchParams.ServiceType.Value);
        }

        // Date range filters
        if (searchParams.CreatedFrom.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= searchParams.CreatedFrom.Value);
        }

        if (searchParams.CreatedTo.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= searchParams.CreatedTo.Value);
        }

        // City filter (shipper OR recipient)
        if (!string.IsNullOrWhiteSpace(searchParams.City))
        {
            var city = searchParams.City.Trim();
            query = query.Where(p =>
                p.ShipperAddress.City == city ||
                p.RecipientAddress.City == city);
        }

        // Country filter (shipper OR recipient)
        if (!string.IsNullOrWhiteSpace(searchParams.Country))
        {
            var country = searchParams.Country.Trim();
            query = query.Where(p =>
                p.ShipperAddress.CountryCode == country ||
                p.RecipientAddress.CountryCode == country);
        }

        // Keyword search (tracking number OR description with case-insensitive matching)
        // Note: Case-insensitive comparison will be handled by the database provider
        if (!string.IsNullOrWhiteSpace(searchParams.Keyword))
        {
            var keyword = searchParams.Keyword.Trim().ToLower();
            query = query.Where(p =>
                p.TrackingNumber.ToLower().Contains(keyword) ||
                (p.Description != null && p.Description.ToLower().Contains(keyword)));
        }

        return query;
    }

    /// <summary>
    /// Applies sorting to a parcel query based on the provided search parameters.
    /// Always includes Id as a tiebreaker to ensure deterministic ordering.
    /// </summary>
    /// <param name="query">The IQueryable to sort</param>
    /// <param name="searchParams">The search parameters containing sort criteria</param>
    /// <returns>A sorted IQueryable</returns>
    public static IQueryable<Parcel> ApplySorting(
        IQueryable<Parcel> query,
        ParcelSearchParams searchParams)
    {
        var sortBy = searchParams.SortBy?.ToLower() ?? "createdat";
        var descending = searchParams.SortDescending;

        return sortBy switch
        {
            "createdat" => descending
                ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),

            "estimateddeliverydate" => descending
                ? query.OrderByDescending(p => p.EstimatedDeliveryDate).ThenBy(p => p.Id)
                : query.OrderBy(p => p.EstimatedDeliveryDate).ThenBy(p => p.Id),

            "status" => descending
                ? query.OrderByDescending(p => p.Status).ThenBy(p => p.Id)
                : query.OrderBy(p => p.Status).ThenBy(p => p.Id),

            // Default to createdAt descending for unknown sort fields
            _ => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
    }

    /// <summary>
    /// Applies cursor-based pagination to a parcel query.
    /// Decodes the cursor and applies a WHERE clause to fetch items after the cursor position.
    /// Handles invalid cursors gracefully by returning the query unchanged (first page).
    /// </summary>
    /// <param name="query">The sorted IQueryable to paginate</param>
    /// <param name="searchParams">The search parameters containing cursor and sort information</param>
    /// <returns>An IQueryable filtered to items after the cursor position</returns>
    public static IQueryable<Parcel> ApplyCursor(
        IQueryable<Parcel> query,
        ParcelSearchParams searchParams)
    {
        // If no cursor provided, return query unchanged (first page)
        if (string.IsNullOrWhiteSpace(searchParams.Cursor))
        {
            return query;
        }

        try
        {
            // Decode the cursor
            var (sortField, sortValue, id) = CursorHelper.Decode(searchParams.Cursor);

            var sortBy = searchParams.SortBy?.ToLower() ?? "createdat";
            var descending = searchParams.SortDescending;

            // Apply cursor WHERE clause based on sort field and direction
            return sortBy switch
            {
                "createdat" => ApplyCursorForCreatedAt(query, sortValue, id, descending),
                "estimateddeliverydate" => ApplyCursorForEstimatedDeliveryDate(query, sortValue, id, descending),
                "status" => ApplyCursorForStatus(query, sortValue, id, descending),
                _ => ApplyCursorForCreatedAt(query, sortValue, id, descending) // Default
            };
        }
        catch (FormatException)
        {
            // Invalid or tampered cursor: gracefully fall back to first page
            return query;
        }
    }

    private static IQueryable<Parcel> ApplyCursorForCreatedAt(
        IQueryable<Parcel> query,
        string sortValue,
        int id,
        bool descending)
    {
        if (!DateTimeOffset.TryParse(sortValue, out var cursorCreatedAt))
        {
            return query; // Invalid date format, return first page
        }

        if (descending)
        {
            // For descending: fetch items where (CreatedAt < cursor) OR (CreatedAt = cursor AND Id > cursor.Id)
            return query.Where(p =>
                p.CreatedAt < cursorCreatedAt ||
                (p.CreatedAt == cursorCreatedAt && p.Id > id));
        }
        else
        {
            // For ascending: fetch items where (CreatedAt > cursor) OR (CreatedAt = cursor AND Id > cursor.Id)
            return query.Where(p =>
                p.CreatedAt > cursorCreatedAt ||
                (p.CreatedAt == cursorCreatedAt && p.Id > id));
        }
    }

    private static IQueryable<Parcel> ApplyCursorForEstimatedDeliveryDate(
        IQueryable<Parcel> query,
        string sortValue,
        int id,
        bool descending)
    {
        // Handle null values in sortValue (for nullable EstimatedDeliveryDate)
        DateTimeOffset? cursorDate = null;
        if (!string.IsNullOrEmpty(sortValue) && sortValue != "null")
        {
            if (!DateTimeOffset.TryParse(sortValue, out var parsedDate))
            {
                return query; // Invalid date format, return first page
            }
            cursorDate = parsedDate;
        }

        if (descending)
        {
            // For descending: fetch items where (EstimatedDeliveryDate < cursor) OR (EstimatedDeliveryDate = cursor AND Id > cursor.Id)
            // Nulls are sorted last in descending order
            return query.Where(p =>
                (cursorDate.HasValue && p.EstimatedDeliveryDate < cursorDate) ||
                (p.EstimatedDeliveryDate == cursorDate && p.Id > id) ||
                (!cursorDate.HasValue && p.EstimatedDeliveryDate != null));
        }
        else
        {
            // For ascending: fetch items where (EstimatedDeliveryDate > cursor) OR (EstimatedDeliveryDate = cursor AND Id > cursor.Id)
            // Nulls are sorted first in ascending order
            return query.Where(p =>
                (cursorDate.HasValue && p.EstimatedDeliveryDate > cursorDate) ||
                (p.EstimatedDeliveryDate == cursorDate && p.Id > id) ||
                (cursorDate.HasValue && !p.EstimatedDeliveryDate.HasValue));
        }
    }

    private static IQueryable<Parcel> ApplyCursorForStatus(
        IQueryable<Parcel> query,
        string sortValue,
        int id,
        bool descending)
    {
        if (!Enum.TryParse<ParcelTracking.Domain.Enums.ParcelStatus>(sortValue, out var cursorStatus))
        {
            return query; // Invalid status format, return first page
        }

        if (descending)
        {
            // For descending: fetch items where (Status < cursor) OR (Status = cursor AND Id > cursor.Id)
            return query.Where(p =>
                p.Status < cursorStatus ||
                (p.Status == cursorStatus && p.Id > id));
        }
        else
        {
            // For ascending: fetch items where (Status > cursor) OR (Status = cursor AND Id > cursor.Id)
            return query.Where(p =>
                p.Status > cursorStatus ||
                (p.Status == cursorStatus && p.Id > id));
        }
    }

    /// <summary>
    /// Builds a PagedResult from a list of parcels fetched with pageSize + 1 logic.
    /// Generates pagination metadata including nextCursor and hasNextPage flag.
    /// Maps Parcel entities to ParcelSearchResponse DTOs.
    /// </summary>
    /// <param name="parcels">The list of parcels (fetched with pageSize + 1)</param>
    /// <param name="searchParams">The search parameters containing pagination settings</param>
    /// <param name="totalCount">The total count of matching records before pagination</param>
    /// <returns>A PagedResult with items and pagination metadata</returns>
    public static PagedResult<ParcelSearchResponse> BuildPagedResult(
        List<Parcel> parcels,
        ParcelSearchParams searchParams,
        int totalCount)
    {
        var pageSize = searchParams.PageSize;

        // Determine if there's a next page by checking if we got more items than requested
        var hasNextPage = parcels.Count > pageSize;

        // Take only the requested page size (remove the extra item used for detection)
        var itemsForPage = hasNextPage ? parcels.Take(pageSize).ToList() : parcels;

        // Generate nextCursor from the last item on the page
        string? nextCursor = null;
        if (hasNextPage && itemsForPage.Count > 0)
        {
            var lastItem = itemsForPage[itemsForPage.Count - 1];
            var sortBy = searchParams.SortBy?.ToLower() ?? "createdat";

            // Get the sort value based on the sort field
            var sortValue = sortBy switch
            {
                "createdat" => lastItem.CreatedAt.ToString("O"),
                "estimateddeliverydate" => lastItem.EstimatedDeliveryDate?.ToString("O") ?? "null",
                "status" => lastItem.Status.ToString(),
                _ => lastItem.CreatedAt.ToString("O") // Default
            };

            nextCursor = CursorHelper.Encode(sortBy, sortValue, lastItem.Id);
        }

        // Map entities to ParcelSearchResponse DTOs
        var items = itemsForPage.Select(p => new ParcelSearchResponse
        {
            Id = p.Id,
            TrackingNumber = p.TrackingNumber,
            Status = p.Status.ToString(),
            ServiceType = p.ServiceType.ToString(),
            ShipperCity = p.ShipperAddress.City,
            RecipientCity = p.RecipientAddress.City,
            CreatedAt = p.CreatedAt,
            EstimatedDeliveryDate = p.EstimatedDeliveryDate
        }).ToList();

        return new PagedResult<ParcelSearchResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageSize = pageSize,
            Cursor = searchParams.Cursor,
            NextCursor = nextCursor,
            HasNextPage = hasNextPage
        };
    }
}
