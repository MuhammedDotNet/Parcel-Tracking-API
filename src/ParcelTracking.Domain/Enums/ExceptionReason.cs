namespace ParcelTracking.Domain.Enums;

public enum ExceptionReason
{
    AddressNotFound = 0,
    RecipientUnavailable = 1,
    DamagedInTransit = 2,
    WeatherDelay = 3,
    CustomsHold = 4,
    RefusedByRecipient = 5
}