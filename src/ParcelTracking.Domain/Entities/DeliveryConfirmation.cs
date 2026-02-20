using System.ComponentModel.DataAnnotations;

namespace ParcelTracking.Domain.Entities;

public class DeliveryConfirmation
{
    public int Id { get; set; }

    public int ParcelId { get; set; }
    public Parcel Parcel { get; set; } = null!;

    [MaxLength(200)]
    public string? ReceivedBy { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    public string? SignatureImage { get; set; }

    public DateTimeOffset DeliveredAt { get; set; }
}