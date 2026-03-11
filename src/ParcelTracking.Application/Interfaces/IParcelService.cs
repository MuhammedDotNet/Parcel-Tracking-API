using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

public interface IParcelService
{
    Task<Parcel?> GetWritableParcelAsync(int parcelId, CancellationToken ct = default);
    Task<Parcel> UpdateParcelDetailsAsync(int parcelId, ParcelPatchModel patchModel, CancellationToken ct = default);
    Task<PagedResult<ParcelSearchResponse>> SearchParcelsAsync(ParcelSearchParams searchParams, CancellationToken ct = default);
}
