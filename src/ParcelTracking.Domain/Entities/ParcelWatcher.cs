using System.ComponentModel.DataAnnotations;
namespace ParcelTracking.Domain.Entities;

public class ParcelWatcher
{
    public int Id { get; set; }

    [Required]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Many-to-many navigation
    public ICollection<Parcel> Parcels { get; set; } = new List<Parcel>();
}