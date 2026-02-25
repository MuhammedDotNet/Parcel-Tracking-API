using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Api.IntegrationTests;

public class ParcelTrackingWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"ParcelTracking_Test_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ParcelTrackingDbContext>) ||
                    d.ServiceType == typeof(ParcelTrackingDbContext) ||
                    d.ServiceType.FullName?.Contains("DbContextOptions") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // Use InMemory database for integration tests
            services.AddDbContext<ParcelTrackingDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
