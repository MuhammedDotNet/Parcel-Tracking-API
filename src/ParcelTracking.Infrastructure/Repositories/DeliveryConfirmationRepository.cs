using Microsoft.EntityFrameworkCore;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IDeliveryConfirmationRepository"/>.
/// </summary>
public sealed class DeliveryConfirmationRepository : IDeliveryConfirmationRepository
{
    private readonly ParcelTrackingDbContext _db;

    public DeliveryConfirmationRepository(ParcelTrackingDbContext db) => _db = db;

    public Task<DeliveryConfirmation?> GetByParcelIdAsync(int parcelId, CancellationToken ct = default)
        => _db.DeliveryConfirmations.FirstOrDefaultAsync(dc => dc.ParcelId == parcelId, ct);

    public Task<bool> ExistsForParcelAsync(int parcelId, CancellationToken ct = default)
        => _db.DeliveryConfirmations.AnyAsync(dc => dc.ParcelId == parcelId, ct);

    public async Task AddAsync(DeliveryConfirmation confirmation, CancellationToken ct = default)
        => await _db.DeliveryConfirmations.AddAsync(confirmation, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
