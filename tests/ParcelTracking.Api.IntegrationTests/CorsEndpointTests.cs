using FluentAssertions;
using System.Net;

namespace ParcelTracking.Api.IntegrationTests;

/// <summary>
/// Integration tests for CORS functionality.
/// Tests Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6
/// </summary>
public class CorsEndpointTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public CorsEndpointTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task PreflightRequest_ReturnsCorrespondingCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/parcels");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
    }

    [Fact]
    public async Task DevelopmentEnvironment_AllowsAnyOrigin()
    {
        // Arrange - factory is configured for Testing environment (which also allows any origin)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/analytics/pipeline");
        request.Headers.Add("Origin", "https://random-origin.com");
        // Bypass response cache so CORS is evaluated fresh
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        // Note: X-Api-Key already in _client.DefaultRequestHeaders; adding again would duplicate it.

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        allowedOrigin.Should().Be("*", "Testing/Development environment should allow any origin");
    }

    [Fact]
    public async Task CorsHeaders_IncludeExpectedMethods()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/parcels");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
        var allowedMethods = response.Headers.GetValues("Access-Control-Allow-Methods").FirstOrDefault();
        allowedMethods.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CorsHeaders_IncludeExpectedHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/parcels");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type,Authorization");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").FirstOrDefault();
        allowedHeaders.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ActualRequest_WithOriginHeader_IncludesCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/analytics/pipeline");
        request.Headers.Add("Origin", "https://example.com");
        // Bypass response cache so we get a fresh response with CORS headers evaluated
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        // Note: X-Api-Key is already set in _client.DefaultRequestHeaders; don't add it
        // again here as it would create a duplicate ("key, key") and fail authentication.

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }
}
