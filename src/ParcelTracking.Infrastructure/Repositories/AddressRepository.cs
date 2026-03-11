using Microsoft.EntityFrameworkCore;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly ParcelTrackingDbContext _db;

    public AddressRepository(ParcelTrackingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Address>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Addresses.AsNoTracking().ToListAsync(ct);
    }

    public async Task<Address?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _db.Addresses.FindAsync(new object?[] { id }, ct);
    }

    public async Task<Address> AddAsync(Address entity, CancellationToken ct)
    {
        _db.Addresses.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountParcelReferencesAsync(int addressId, CancellationToken ct)
    {
        return await _db.Parcels
            .CountAsync(p => p.ShipperAddressId == addressId || p.RecipientAddressId == addressId, ct);
    }

    public void Remove(Address entity)
    {
        _db.Addresses.Remove(entity);
    }
}
