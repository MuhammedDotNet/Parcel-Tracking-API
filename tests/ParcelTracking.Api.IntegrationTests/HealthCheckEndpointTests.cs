using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Infrastructure.Data;
using Xunit;

namespace ParcelTracking.Api.IntegrationTests;

[Collection("Database")]
public class HealthCheckEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFixture _fixture;

    public HealthCheckEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_WithHealthyDatabase_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsStructuredJson()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        Assert.True(json.RootElement.TryGetProperty("status", out _));
        Assert.True(json.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(json.RootElement.TryGetProperty("totalDurationMs", out _));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
    }

    [Fact]
    public async Task HealthReady_IncludesDatabaseCheck()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        var checks = json.RootElement.GetProperty("checks");
        var checkArray = checks.EnumerateArray().ToList();
        
        Assert.NotEmpty(checkArray);
        var databaseCheck = checkArray.FirstOrDefault(c => 
            c.TryGetProperty("name", out var name) && name.GetString() == "database");
        
        Assert.True(databaseCheck.ValueKind != JsonValueKind.Undefined);
        Assert.True(databaseCheck.TryGetProperty("status", out _));
        Assert.True(databaseCheck.TryGetProperty("description", out _));
        Assert.True(databaseCheck.TryGetProperty("durationMs", out _));
    }

    [Fact]
    public async Task HealthChecks_AreNotRateLimited()
    {
        // Act - Make multiple requests in quick succession
        var tasks = Enumerable.Range(0, 150)
            .Select(_ => _client.GetAsync("/health/live"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed (not rate limited)
        Assert.All(responses, response => 
            Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
