namespace ParcelTracking.Domain.Enums;

public enum EventType
{
    LabelCreated = 0,
    PickedUp = 1,
    ArrivedAtFacility = 2,
    DepartedFacility = 3,
    InTransit = 4,
    OutForDelivery = 5,
    Delivered = 6,
    DeliveryAttempted = 7,
    Exception = 8,
    Returned = 9,
    AddressCorrection = 10,
    CustomsClearance = 11,
    HeldAtFacility = 12
}