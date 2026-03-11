using ParcelTracking.Domain.Entities;

namespace ParcelTracking.Application.Interfaces;

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> GetAllAsync(CancellationToken ct);
    Task<Address?> GetByIdAsync(int id, CancellationToken ct);
    Task<Address> AddAsync(Address entity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<int> CountParcelReferencesAsync(int addressId, CancellationToken ct);
    void Remove(Address entity);
}
