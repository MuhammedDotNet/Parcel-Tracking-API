using System.Text;

namespace ParcelTracking.Application.Helpers;

public static class CursorHelper
{
    public static string Encode(string sortField, string sortValue, int id)
    {
        var cursorString = $"{sortField}|{sortValue}|{id}";
        var bytes = Encoding.UTF8.GetBytes(cursorString);
        return Convert.ToBase64String(bytes);
    }

    public static (string SortField, string SortValue, int Id) Decode(string cursor)
    {
        var bytes = Convert.FromBase64String(cursor);
        var cursorString = Encoding.UTF8.GetString(bytes);
        var parts = cursorString.Split('|');
        
        if (parts.Length != 3)
        {
            throw new FormatException("Invalid cursor format");
        }

        return (parts[0], parts[1], int.Parse(parts[2]));
    }
}
