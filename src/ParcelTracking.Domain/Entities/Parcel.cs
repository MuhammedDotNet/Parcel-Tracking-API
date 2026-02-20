using ParcelTracking.Domain.Enums;
using System.ComponentModel.DataAnnotations;
namespace ParcelTracking.Domain.Entities;

public class Parcel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string TrackingNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ServiceType ServiceType { get; set; }
    public ParcelStatus Status { get; set; }

    // Address relationships
    public int ShipperAddressId { get; set; }
    public Address ShipperAddress { get; set; } = null!;

    public int RecipientAddressId { get; set; }
    public Address RecipientAddress { get; set; } = null!;

    // Physical properties
    public decimal Weight { get; set; }
    public WeightUnit WeightUnit { get; set; }
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public DimensionUnit DimensionUnit { get; set; }

    // Value
    public decimal DeclaredValue { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    // Dates
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
    public DateTimeOffset? ActualDeliveryDate { get; set; }

    // Delivery tracking
    public int DeliveryAttempts { get; set; }

    // Audit
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
    public ICollection<ParcelContentItem> ContentItems { get; set; } = new List<ParcelContentItem>();
    public DeliveryConfirmation? DeliveryConfirmation { get; set; }
    public ICollection<ParcelWatcher> Watchers { get; set; } = new List<ParcelWatcher>();
}