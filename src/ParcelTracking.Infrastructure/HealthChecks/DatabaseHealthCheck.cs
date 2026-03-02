using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Infrastructure.HealthChecks;

/// <summary>
/// Health check that verifies database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ParcelTrackingDbContext _dbContext;

    public DatabaseHealthCheck(ParcelTrackingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connection is active");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
