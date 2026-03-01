using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsSearchEndpointTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public ParcelsSearchEndpointTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Search_WithNoParameters_ReturnsPagedResult()
    {
        var response = await _client.GetAsync("/api/parcels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(0);
        result.PageSize.Should().Be(20); // Default page size
    }

    [Fact]
    public async Task Search_WithStatusParameter_MapsCorrectly()
    {
        // Seed a parcel with InTransit status
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResponse = await _client.PostAsJsonAsync("/api/parcels", request);
        var createdParcel = await createResponse.Content.ReadFromJsonAsync<ParcelResponse>();

        var response = await _client.GetAsync("/api/parcels?status=LabelCreated");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        // All returned items should have LabelCreated status
        result.Items.Should().AllSatisfy(p => p.Status.Should().Be("LabelCreated"));
    }

    [Fact]
    public async Task Search_WithServiceTypeParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?serviceType=Express");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        // All returned items should have Express service type
        result.Items.Should().AllSatisfy(p => p.ServiceType.Should().Be("Express"));
    }

    [Fact]
    public async Task Search_WithDateRangeParameters_MapsCorrectly()
    {
        var createdFrom = DateTimeOffset.UtcNow.AddDays(-7);
        var createdTo = DateTimeOffset.UtcNow.AddDays(-1);

        // Use URL encoding for DateTimeOffset values
        var createdFromStr = Uri.EscapeDataString(createdFrom.ToString("O"));
        var createdToStr = Uri.EscapeDataString(createdTo.ToString("O"));

        var response = await _client.GetAsync(
            $"/api/parcels?createdFrom={createdFromStr}&createdTo={createdToStr}");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Expected 200 OK but got {response.StatusCode}. Error: {errorContent}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithCityParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?city=Berlin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithCountryParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?country=DE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithKeywordParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?keyword=PKG");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithSortByParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?sortBy=createdAt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithSortDescendingParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?sortDescending=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithPageSizeParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task Search_WithCursorParameter_MapsCorrectly()
    {
        var response = await _client.GetAsync("/api/parcels?cursor=dGVzdA==");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithNullableParameters_HandlesNullCorrectly()
    {
        // Test that omitted parameters are handled as null
        var response = await _client.GetAsync("/api/parcels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Search_WithDefaultValues_UsesDefaults()
    {
        var response = await _client.GetAsync("/api/parcels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(20); // Default page size
    }

    [Fact]
    public async Task Search_WithInvalidDateRange_Returns400()
    {
        var createdFrom = DateTimeOffset.UtcNow;
        var createdTo = DateTimeOffset.UtcNow.AddDays(-7);

        var response = await _client.GetAsync(
            $"/api/parcels?createdFrom={createdFrom:O}&createdTo={createdTo:O}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithPageSizeTooLarge_ClampsTo100()
    {
        var response = await _client.GetAsync("/api/parcels?pageSize=500");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(100); // Clamped to max
    }

    [Fact]
    public async Task Search_WithPageSizeTooSmall_ClampsTo1()
    {
        var response = await _client.GetAsync("/api/parcels?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(1); // Clamped to min
    }

    [Fact]
    public async Task Search_WithNegativePageSize_ClampsTo1()
    {
        var response = await _client.GetAsync("/api/parcels?pageSize=-10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(1); // Clamped to min
    }

    [Fact]
    public async Task Search_WithCacheHeaders_ReturnsCorrectHeaders()
    {
        var response = await _client.GetAsync("/api/parcels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Search_WithMultipleParameters_MapsAllCorrectly()
    {
        var response = await _client.GetAsync(
            "/api/parcels?status=LabelCreated&serviceType=Express&pageSize=10&sortBy=createdAt&sortDescending=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ParcelSearchResponse>>();
        result.Should().NotBeNull();
        result!.PageSize.Should().Be(10);
    }
}
