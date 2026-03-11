using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Interfaces;

public interface IParcelStatusService
{
    Task<StatusTransitionResult> TransitionStatusAsync(
        int parcelId,
        ParcelStatus newStatus,
        CancellationToken ct = default);
}
