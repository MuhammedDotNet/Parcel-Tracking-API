namespace ParcelTracking.Application.DTOs;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public string? Cursor { get; set; }
    public string? NextCursor { get; set; }
    public bool HasNextPage { get; set; }
}
