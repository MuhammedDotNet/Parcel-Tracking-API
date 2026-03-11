using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Application.Mappings;

namespace ParcelTracking.Application.Services;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _repository;

    public AddressService(IAddressRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AddressResponse>> GetAllAsync(CancellationToken ct)
    {
        var addresses = await _repository.GetAllAsync(ct);
        return addresses.Select(a => a.ToResponse()).ToList();
    }

    public async Task<AddressResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        return entity?.ToResponse();
    }

    public async Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct)
    {
        var entity = request.ToEntity();
        await _repository.AddAsync(entity, ct);
        return entity.ToResponse();
    }

    public async Task<AddressResponse?> UpdateAsync(int id, UpdateAddressRequest request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return null;

        entity.ApplyUpdate(request);
        await _repository.SaveChangesAsync(ct);
        return entity.ToResponse();
    }

    public async Task<DeleteResult> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null)
            return new DeleteResult { NotFound = true };

        var parcelCount = await _repository.CountParcelReferencesAsync(id, ct);
        if (parcelCount > 0)
            return new DeleteResult
            {
                Conflict = true,
                ConflictMessage = $"Cannot delete address. It is referenced by {parcelCount} parcel(s)."
            };

        _repository.Remove(entity);
        await _repository.SaveChangesAsync(ct);
        return new DeleteResult();
    }
}
