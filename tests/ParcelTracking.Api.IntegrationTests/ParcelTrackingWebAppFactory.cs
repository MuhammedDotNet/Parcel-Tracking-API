using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Api.IntegrationTests;

public class ParcelTrackingWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ParcelTrackingWebAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inject test-specific configuration values (API key, connection strings, etc.)
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:ApiKey"] = "dev-api-key-12345"
            });
        });

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

            // Use real PostgreSQL from Testcontainers
            services.AddDbContext<ParcelTrackingDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }
}
