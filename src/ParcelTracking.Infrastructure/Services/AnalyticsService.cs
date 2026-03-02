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

    public async Task<DeliveryStatsResponse> GetDeliveryStatsAsync(DateTimeOffset from, DateTimeOffset to)
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
            .ToListAsync();

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
            .ToListAsync();

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
                              && p.ActualDeliveryDate <= p.EstimatedDeliveryDate);

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

    public async Task<List<ExceptionReasonResponse>> GetTopExceptionReasonsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var exceptionEvents = _context.TrackingEvents
            .Where(e => e.EventType == EventType.Exception
                     && e.DelayReason != null
                     && e.Timestamp >= from
                     && e.Timestamp <= to);

        var totalExceptions = await exceptionEvents.CountAsync();

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
            .ToListAsync();

        // Compute percentages in memory
        var reasons = grouped.Select(g => new ExceptionReasonResponse
        {
            Reason = g.Reason!,
            Count = g.Count,
            Percentage = Math.Round((double)g.Count / totalExceptions * 100, 1)
        }).ToList();

        return reasons;
    }

    public async Task<List<ServiceBreakdownResponse>> GetServiceBreakdownAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var breakdown = await _context.Parcels
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .GroupBy(p => p.ServiceType)
            .Select(g => new
            {
                ServiceType = g.Key,
                Count = g.Count(),
                DeliveredCount = g.Count(p =>
                    p.Status == ParcelStatus.Delivered
                    && p.ActualDeliveryDate != null),
                TotalDeliveryHours = g
                    .Where(p => p.Status == ParcelStatus.Delivered
                             && p.ActualDeliveryDate != null)
                    .Sum(p => (p.ActualDeliveryDate!.Value - p.CreatedAt).TotalHours)
            })
            .ToListAsync();

        return breakdown.Select(b => new ServiceBreakdownResponse
        {
            ServiceType = b.ServiceType.ToString(),
            Count = b.Count,
            AverageDeliveryTimeHours = b.DeliveredCount > 0
                ? Math.Round(b.TotalDeliveryHours / b.DeliveredCount, 1)
                : 0
        }).ToList();
    }

    public async Task<List<PipelineStatusResponse>> GetPipelineAsync()
    {
        // Query all parcels and group by status
        var counts = await _context.Parcels
            .GroupBy(p => p.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

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
