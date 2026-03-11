using System.ComponentModel.DataAnnotations;
namespace ParcelTracking.Domain.Entities;

public class Address
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Street1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Street2 { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    public string CountryCode { get; set; } = string.Empty;

    public bool IsResidential { get; set; }

    [Required]
    [MaxLength(150)]
    public string ContactName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(254)]
    public string? Email { get; set; }
}