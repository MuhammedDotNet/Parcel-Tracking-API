using ParcelTracking.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ParcelTracking.Domain.Entities;

public class ParcelContentItem
{
    public int Id { get; set; }

    public int ParcelId { get; set; }
    public Parcel Parcel { get; set; } = null!;

    [Required]
    [MaxLength(7)]
    public string HsCode { get; set; } = string.Empty; // Format: XXXX.XX

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitValue { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD"; // ISO 4217

    public decimal Weight { get; set; }
    public WeightUnit WeightUnit { get; set; }

    [Required]
    [MaxLength(2)]
    public string CountryOfOrigin { get; set; } = string.Empty; // ISO 3166-1 alpha-2
}