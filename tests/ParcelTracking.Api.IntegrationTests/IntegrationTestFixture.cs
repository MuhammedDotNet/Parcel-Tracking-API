using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ParcelTracking.Infrastructure.Data;
using Respawn;
using Testcontainers.PostgreSql;

namespace ParcelTracking.Api.IntegrationTests;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("parceltracking_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private Respawner _respawner = default!;
    private string _connectionString = default!;

    public ParcelTrackingWebAppFactory Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        await _postgres.StartAsync();

        _connectionString = _postgres.GetConnectionString();

        Factory = new ParcelTrackingWebAppFactory(_connectionString);

        // Trigger the host to start so EF Core applies migrations / creates schema
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
        await db.Database.EnsureCreatedAsync();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        await _postgres.DisposeAsync();
    }

    // Explicit interface implementation for xUnit IAsyncLifetime
    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
