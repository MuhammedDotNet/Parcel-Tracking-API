using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsRegistrationTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public ParcelsRegistrationTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Register_WithValidRequest_Returns201WithTrackingNumber()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var parcel = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        parcel.Should().NotBeNull();
        parcel!.TrackingNumber.Should().StartWith("PKG-");
        parcel.Status.Should().Be("LabelCreated");
        parcel.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_WithValidRequest_LocationHeaderIsSet()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().ToLowerInvariant().Should().Contain("parcels");
    }

    [Fact]
    public async Task Register_WhenShipperAddressNotFound_Returns404()
    {
        var request = ParcelTestHelpers.BuildRequest(shipperId: 999999, recipientId: 999998);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_WhenRecipientAddressNotFound_Returns404()
    {
        var (shipperId, _) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId: 999997);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_WithEmptyContentItems_Returns400()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId) with { ContentItems = [] };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidHsCode_Returns400()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId) with
        {
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode = "84713",
                    Description = "Laptop",
                    Quantity = 1,
                    UnitValue = 100m,
                    Currency = "USD",
                    Weight = 1m,
                    WeightUnit = "kg",
                    CountryOfOrigin = "CN"
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidServiceType_Returns400()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId) with { ServiceType = "Turbo" };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithoutApiKey_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);

        var response = await anonClient.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ExpressService_EstimatedDeliveryAtLeast3DaysOut()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId) with { ServiceType = "Express" };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var parcel = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        parcel!.EstimatedDeliveryDate.Should().NotBeNull();
        parcel.EstimatedDeliveryDate!.Value.Should()
            .BeAfter(DateTimeOffset.UtcNow.AddDays(2),
                because: "Express service takes at least 3 business days");
    }

    [Fact]
    public async Task Register_ReturnsCorrectContentItemsInResponse()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        var parcel = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        parcel!.ContentItems.Should().HaveCount(1);
        parcel.ContentItems[0].HsCode.Should().Be("8471.30");
        parcel.ContentItems[0].CountryOfOrigin.Should().Be("CN");
    }
}
