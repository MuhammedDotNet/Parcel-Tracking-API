using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

public interface IParcelRetrievalService
{
    Task<ParcelDetailResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<TrackingResponse?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct);
}
