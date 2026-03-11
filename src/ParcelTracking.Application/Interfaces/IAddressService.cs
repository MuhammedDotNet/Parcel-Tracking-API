using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Interfaces;

public interface IAddressService
{
    Task<IReadOnlyList<AddressResponse>> GetAllAsync(CancellationToken ct);
    Task<AddressResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct);
    Task<AddressResponse?> UpdateAsync(int id, UpdateAddressRequest request, CancellationToken ct);
    Task<DeleteResult> DeleteAsync(int id, CancellationToken ct);
}
