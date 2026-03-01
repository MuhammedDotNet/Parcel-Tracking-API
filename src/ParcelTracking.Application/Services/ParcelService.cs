using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Exceptions;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.Services;

public class ParcelService : IParcelService
{
    private readonly IParcelRepository _parcelRepository;

    public ParcelService(IParcelRepository parcelRepository)
    {
        _parcelRepository = parcelRepository;
    }

    public async Task<Parcel?> GetWritableParcelAsync(int parcelId, CancellationToken ct = default)
    {
        var parcel = await _parcelRepository.GetByIdAsync(parcelId, ct);

        if (parcel is null)
        {
            return null;
        }

        if (ParcelStatusRules.IsTerminal(parcel.Status))
        {
            throw new ParcelInTerminalStateException(parcel.Id, parcel.Status);
        }

        return parcel;
    }

    public async Task<Parcel> UpdateParcelDetailsAsync(
        int parcelId,
        ParcelPatchModel patchModel,
        CancellationToken ct = default)
    {
        var parcel = await _parcelRepository.GetByIdAsync(parcelId, ct);

        if (parcel is null)
        {
            throw new InvalidOperationException($"Parcel with ID {parcelId} not found");
        }

        // Update non-status fields
        parcel.ServiceType = patchModel.ServiceType;
        parcel.Description = patchModel.Description;
        parcel.EstimatedDeliveryDate = patchModel.EstimatedDeliveryDate;
        parcel.UpdatedAt = DateTimeOffset.UtcNow;

        await _parcelRepository.SaveChangesAsync(ct);

        return parcel;
    }
}
