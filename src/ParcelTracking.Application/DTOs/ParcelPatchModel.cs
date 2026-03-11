using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.DTOs;

public class ParcelPatchModel
{
    [JsonConverter(typeof(StringEnumConverter))]
    public ParcelStatus Status { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ServiceType ServiceType { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
}
