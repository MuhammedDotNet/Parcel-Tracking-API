using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

public interface IDeliveryEstimationService
{
    DeliveryEstimateResult Calculate(Parcel parcel);
    DeliveryEstimateResult Recalculate(Parcel parcel, DateOnly fromDate);
}
