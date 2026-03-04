using Microsoft.EntityFrameworkCore;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Interfaces;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ParcelTrackingDbContext _context;

    public AnalyticsService(ParcelTrackingDbContext context)
    {
        _context = context;
    }

    public async Task<DeliveryStatsResponse> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var query = _context.Parcels
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to);

        // Single GroupBy query for all status counts
        var statusCounts = await query
            .GroupBy(p => p.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(ct);

        var totalParcels = statusCounts.Sum(s => s.Count);
        var delivered = statusCounts
            .FirstOrDefault(s => s.Status == ParcelStatus.Delivered)?.Count ?? 0;
        var inTransit = statusCounts
            .Where(s => s.Status == ParcelStatus.InTransit
                      || s.Status == ParcelStatus.OutForDelivery
                      || s.Status == ParcelStatus.PickedUp)
            .Sum(s => s.Count);
        var exceptions = statusCounts
            .FirstOrDefault(s => s.Status == ParcelStatus.Exception)?.Count ?? 0;

        // Average delivery time (only for delivered parcels)
        double averageDeliveryTimeHours = 0;
        var deliveredParcels = await query
            .Where(p => p.Status == ParcelStatus.Delivered
                     && p.ActualDeliveryDate != null)
            .Select(p => new
            {
                p.CreatedAt,
                p.ActualDeliveryDate
            })
            .ToListAsync(ct);

        if (deliveredParcels.Any())
        {
            var totalHours = deliveredParcels
                .Sum(p => (p.ActualDeliveryDate!.Value - p.CreatedAt).TotalHours);
            averageDeliveryTimeHours = totalHours / deliveredParcels.Count;
        }

        // On-time percentage
        double onTimePercentage = 0;
        if (delivered > 0)
        {
            var onTimeCount = await query
                .CountAsync(p => p.Status == ParcelStatus.Delivered
                              && p.ActualDeliveryDate != null
                              && p.EstimatedDeliveryDate != null
                              && p.ActualDeliveryDate <= p.EstimatedDeliveryDate, ct);

            onTimePercentage = Math.Round((double)onTimeCount / delivered * 100, 1);
        }

        return new DeliveryStatsResponse
        {
            From = from,
            To = to,
            TotalParcels = totalParcels,
            Delivered = delivered,
            InTransit = inTransit,
            Exceptions = exceptions,
            AverageDeliveryTimeHours = Math.Round(averageDeliveryTimeHours, 1),
            OnTimePercentage = onTimePercentage
        };
    }

    public async Task<List<ExceptionReasonResponse>> GetTopExceptionReasonsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var exceptionEvents = _context.TrackingEvents
            .Where(e => e.EventType == EventType.Exception
                     && e.DelayReason != null
                     && e.Timestamp >= from
                     && e.Timestamp <= to);

        var totalExceptions = await exceptionEvents.CountAsync(ct);

        if (totalExceptions == 0)
            return new List<ExceptionReasonResponse>();

        // Group by reason and compute counts
        var grouped = await exceptionEvents
            .GroupBy(e => e.DelayReason)
            .Select(g => new
            {
                Reason = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        // Compute percentages in memory
        var reasons = grouped.Select(g => new ExceptionReasonResponse
        {
            Reason = g.Reason!,
            Count = g.Count,
            Percentage = Math.Round((double)g.Count / totalExceptions * 100, 1)
        }).ToList();

        return reasons;
    }

    public async Task<List<ServiceBreakdownResponse>> GetServiceBreakdownAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // Fetch raw data from DB — compute TotalHours in memory to avoid provider-specific SQL translation issues
        var rawData = await _context.Parcels
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .GroupBy(p => p.ServiceType)
            .Select(g => new
            {
                ServiceType = g.Key,
                Count = g.Count(),
                DeliveredParcels = g
                    .Where(p => p.Status == ParcelStatus.Delivered && p.ActualDeliveryDate != null)
                    .Select(p => new { p.CreatedAt, p.ActualDeliveryDate })
                    .ToList()
            })
            .ToListAsync(ct);

        return rawData.Select(b =>
        {
            var totalHours = b.DeliveredParcels.Sum(p => (p.ActualDeliveryDate!.Value - p.CreatedAt).TotalHours);
            return new ServiceBreakdownResponse
            {
                ServiceType = b.ServiceType.ToString(),
                Count = b.Count,
                AverageDeliveryTimeHours = b.DeliveredParcels.Count > 0
                    ? Math.Round(totalHours / b.DeliveredParcels.Count, 1)
                    : 0
            };
        }).ToList();
    }

    public async Task<List<PipelineStatusResponse>> GetPipelineAsync(CancellationToken ct = default)
    {
        // Query all parcels and group by status
        var counts = await _context.Parcels
            .GroupBy(p => p.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(ct);

        // Get all ParcelStatus enum values
        var allStatuses = Enum.GetValues<ParcelStatus>();

        // Fill in missing statuses with zero counts and return all statuses
        return allStatuses.Select(status => new PipelineStatusResponse
        {
            Status = status.ToString(),
            Count = counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0
        })
        .OrderByDescending(p => p.Count)
        .ToList();
    }
}

