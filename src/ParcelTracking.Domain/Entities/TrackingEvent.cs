using ParcelTracking.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ParcelTracking.Domain.Entities;

public class TrackingEvent
{
    public int Id { get; set; }

    public int ParcelId { get; set; }
    public Parcel Parcel { get; set; } = null!;

    public DateTimeOffset Timestamp { get; set; }
    public EventType EventType { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LocationCity { get; set; }

    [MaxLength(100)]
    public string? LocationState { get; set; }

    [MaxLength(100)]
    public string? LocationCountry { get; set; }

    [MaxLength(500)]
    public string? DelayReason { get; set; }
}