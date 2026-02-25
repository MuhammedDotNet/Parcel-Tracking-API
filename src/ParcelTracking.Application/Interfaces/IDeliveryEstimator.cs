namespace ParcelTracking.Application.Interfaces;

public interface IDeliveryEstimator
{
    DateTimeOffset Estimate(string serviceType, DateTimeOffset registrationDate);
}
