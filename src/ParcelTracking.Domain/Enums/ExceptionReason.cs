using System.Text.Json.Serialization;

namespace ParcelTracking.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExceptionReason
{
    AddressNotFound,
    RecipientUnavailable,
    DamagedPackage,
    WeatherDelay,
    CustomsHold,
    RefusedByRecipient
}