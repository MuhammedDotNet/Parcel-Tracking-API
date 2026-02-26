using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

public interface ITrackingService
{
    Task<TrackingEventResponse> AddEventAsync(
        int parcelId, 
        CreateTrackingEventRequest request, 
        CancellationToken ct);
    
    Task<IEnumerable<TrackingEventResponse>> GetHistoryAsync(
        int parcelId, 
        DateTimeOffset? from, 
        DateTimeOffset? to, 
        CancellationToken ct);
}
