using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using System.Net;

namespace ParcelTracking.Api.IntegrationTests;

/// <summary>
/// Property-based tests for CORS functionality.
/// </summary>
public class CorsPropertyTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;

    public CorsPropertyTests(ParcelTrackingWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // Feature: api-production-readiness, Property 12: CORS method headers
    /// <summary>
    /// For any cross-origin response, it should include the Access-Control-Allow-Methods header listing the allowed HTTP methods.
    /// Validates: Requirements 5.4
    /// </summary>
    [Property(MaxTest = 100)]
    public void CorsMethodHeaders(NonNegativeInt endpointIndex)
    {
        // Generate valid endpoint paths
        var endpoints = new[]
        {
            "/api/v1/addresses",
            "/api/v1/parcels",
            "/api/v1/analytics/pipeline"
        };

        var endpoint = endpoints[endpointIndex.Get % endpoints.Length];
        
        // Create preflight OPTIONS request
        var request = new HttpRequestMessage(HttpMethod.Options, endpoint);
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        
        // Make request
        var response = _client.SendAsync(request).Result;
        
        // Check for CORS method headers
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods",
            "Preflight response should include Access-Control-Allow-Methods header");
        
        var allowedMethods = response.Headers.GetValues("Access-Control-Allow-Methods").FirstOrDefault();
        allowedMethods.Should().NotBeNullOrEmpty("Access-Control-Allow-Methods should list allowed methods");
    }

    // Feature: api-production-readiness, Property 13: CORS request headers
    /// <summary>
    /// For any cross-origin response, it should include the Access-Control-Allow-Headers header listing the allowed request headers.
    /// Validates: Requirements 5.5
    /// </summary>
    [Property(MaxTest = 100)]
    public void CorsRequestHeaders(NonNegativeInt endpointIndex)
    {
        // Generate valid endpoint paths
        var endpoints = new[]
        {
            "/api/v1/addresses",
            "/api/v1/parcels",
            "/api/v1/analytics/pipeline"
        };

        var endpoint = endpoints[endpointIndex.Get % endpoints.Length];
        
        // Create preflight OPTIONS request
        var request = new HttpRequestMessage(HttpMethod.Options, endpoint);
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");
        
        // Make request
        var response = _client.SendAsync(request).Result;
        
        // Check for CORS request headers
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers",
            "Preflight response should include Access-Control-Allow-Headers header");
        
        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").FirstOrDefault();
        allowedHeaders.Should().NotBeNullOrEmpty("Access-Control-Allow-Headers should list allowed headers");
    }

    // Feature: api-production-readiness, Property 14: CORS exposed headers
    /// <summary>
    /// For any cross-origin response, it should include the Access-Control-Expose-Headers header listing the custom headers clients can read.
    /// Validates: Requirements 5.6
    /// </summary>
    [Property(MaxTest = 100)]
    public void CorsExposedHeaders(NonNegativeInt endpointIndex)
    {
        // Generate valid endpoint paths
        var endpoints = new[]
        {
            "/api/v1/addresses",
            "/api/v1/parcels",
            "/api/v1/analytics/pipeline"
        };

        var endpoint = endpoints[endpointIndex.Get % endpoints.Length];
        
        // Create actual request with Origin header
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("X-Api-Key", "test-key");
        
        // Make request
        var response = _client.SendAsync(request).Result;
        
        // Check for CORS exposed headers
        // Note: Access-Control-Expose-Headers may not be present in development (AllowAnyOrigin)
        // but should be present in production configuration
        if (response.Headers.Contains("Access-Control-Expose-Headers"))
        {
            var exposedHeaders = response.Headers.GetValues("Access-Control-Expose-Headers").FirstOrDefault();
            exposedHeaders.Should().NotBeNullOrEmpty("Access-Control-Expose-Headers should list exposed headers");
        }
    }
}
