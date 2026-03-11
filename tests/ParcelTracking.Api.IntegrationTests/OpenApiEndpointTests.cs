using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace ParcelTracking.Api.IntegrationTests;

/// <summary>
/// Integration tests for OpenAPI documentation endpoints
/// </summary>
[Collection("Database")]
public class OpenApiEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public OpenApiEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task OpenApiEndpoint_ReturnsValidJsonDocument()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        // Verify it's valid JSON
        var jsonDoc = JsonDocument.Parse(content);
        Assert.NotNull(jsonDoc);

        // Verify it has OpenAPI structure
        var root = jsonDoc.RootElement;
        Assert.True(root.TryGetProperty("openapi", out var openApiVersion));
        Assert.True(root.TryGetProperty("info", out var info));
        Assert.True(root.TryGetProperty("paths", out var paths));

        // Verify info section
        Assert.True(info.TryGetProperty("title", out var title));
        Assert.Equal("Parcel Tracking API", title.GetString());
        Assert.True(info.TryGetProperty("version", out var version));
        Assert.Equal("v1", version.GetString());
        Assert.True(info.TryGetProperty("description", out var description));
        Assert.Contains("Production-grade REST API", description.GetString());
        Assert.True(info.TryGetProperty("contact", out var contact));
        Assert.True(contact.TryGetProperty("name", out var contactName));
        Assert.Equal("API Support", contactName.GetString());
    }

    [Fact]
    public async Task ScalarUI_IsAccessibleInDevelopment()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/scalar/v1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        
        // Verify it's HTML content
        Assert.Contains("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScalarUI_ReturnsNotFoundInProduction()
    {
        // Arrange
        var client = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/scalar/v1");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_IncludesAllExpectedEndpoints()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var paths = jsonDoc.RootElement.GetProperty("paths");

        // Assert - verify key endpoints are documented
        var pathsList = new List<string>();
        foreach (var path in paths.EnumerateObject())
        {
            pathsList.Add(path.Name.ToLowerInvariant());
        }

        // Check for major endpoint groups (case-insensitive)
        Assert.Contains(pathsList, p => p.Contains("parcels"));
        Assert.Contains(pathsList, p => p.Contains("addresses"));
        Assert.Contains(pathsList, p => p.Contains("tracking"));
        Assert.Contains(pathsList, p => p.Contains("analytics"));
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
