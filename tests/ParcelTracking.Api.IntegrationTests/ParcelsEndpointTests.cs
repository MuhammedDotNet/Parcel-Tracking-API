using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests;

public class ParcelsEndpointTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public ParcelsEndpointTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<(int ShipperId, int RecipientId)> SeedAddressesAsync()
    {
        var shipperResp = await _client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            Street1 = "1 Shipper St",
            City = "ShipCity",
            State = "SC",
            PostalCode = "10001",
            CountryCode = "US",
            IsResidential = false,
            ContactName = "Shipper Corp",
            Phone = "+1-555-0100",
            Email = "ship@test.com"
        });
        shipperResp.EnsureSuccessStatusCode();
        var shipper = await shipperResp.Content.ReadFromJsonAsync<AddressResponse>();

        var recipientResp = await _client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            Street1 = "2 Recipient Ave",
            City = "RecCity",
            State = "RC",
            PostalCode = "20002",
            CountryCode = "US",
            IsResidential = true,
            ContactName = "Jane Recipient",
            Phone = "+1-555-0200",
            Email = "receive@test.com"
        });
        recipientResp.EnsureSuccessStatusCode();
        var recipient = await recipientResp.Content.ReadFromJsonAsync<AddressResponse>();

        return (shipper!.Id, recipient!.Id);
    }

    private static RegisterParcelRequest BuildRequest(int shipperId, int recipientId) => new()
    {
        ShipperAddressId = shipperId,
        RecipientAddressId = recipientId,
        ServiceType = "Express",
        Description = "Electronics - Laptop",
        Weight = new WeightDto { Value = 2.5m, Unit = "kg" },
        Dimensions = new DimensionsDto { Length = 40, Width = 30, Height = 10, Unit = "cm" },
        DeclaredValue = new DeclaredValueDto { Amount = 1200m, Currency = "USD" },
        ContentItems =
        [
            new ContentItemDto
            {
                HsCode          = "8471.30",
                Description     = "Portable laptop computer",
                Quantity        = 1,
                UnitValue       = 1200m,
                Currency        = "USD",
                Weight          = 2.1m,
                WeightUnit      = "kg",
                CountryOfOrigin = "CN"
            }
        ]
    };

    // ─── POST /api/parcels Tests ─────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidRequest_Returns201WithTrackingNumber()
    {
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);

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
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().ToLowerInvariant().Should().Contain("parcels");
    }

    [Fact]
    public async Task Register_WhenShipperAddressNotFound_Returns404()
    {
        var request = BuildRequest(shipperId: 999999, recipientId: 999998);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_WhenRecipientAddressNotFound_Returns404()
    {
        var (shipperId, _) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId: 999997);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_WithEmptyContentItems_Returns400()
    {
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId) with { ContentItems = [] };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidHsCode_Returns400()
    {
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId) with
        {
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode          = "84713",   // Missing dot
                    Description     = "Laptop",
                    Quantity        = 1,
                    UnitValue       = 100m,
                    Currency        = "USD",
                    Weight          = 1m,
                    WeightUnit      = "kg",
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
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId) with { ServiceType = "Turbo" };

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithoutApiKey_Returns401()
    {
        // Create a client without the API key header using the factory (required for in-process test server)
        using var anonClient = _factory.CreateClient();
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);

        var response = await anonClient.PostAsJsonAsync("/api/parcels", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ExpressService_EstimatedDeliveryAtLeast3DaysOut()
    {
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId) with { ServiceType = "Express" };

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
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);

        var response = await _client.PostAsJsonAsync("/api/parcels", request);

        var parcel = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        parcel!.ContentItems.Should().HaveCount(1);
        parcel.ContentItems[0].HsCode.Should().Be("8471.30");
        parcel.ContentItems[0].CountryOfOrigin.Should().Be("CN");
    }

    // ─── GET /api/parcels/{id} Tests ────────────────────────────────────

    [Fact]
    public async Task GetById_WhenExists_Returns200WithDetails()
    {
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);

        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var response = await _client.GetAsync($"/api/parcels/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<ParcelDetailResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(created.Id);
        detail.TrackingNumber.Should().Be(created.TrackingNumber);
        detail.Status.Should().Be("LabelCreated");
        detail.ShipperAddress.Should().NotBeNull();
        detail.RecipientAddress.Should().NotBeNull();
        detail.ContentItems.Should().HaveCount(1);
        detail.IsDelivered.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_WhenNotExists_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/parcels/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Parcel Not Found");
        problem.Status.Should().Be(404);
        problem.Detail.Should().Contain("999999");
    }

    [Fact]
    public async Task GetById_WithoutApiKey_Returns401()
    {
        using var anonClient = _factory.CreateClient();

        var response = await anonClient.GetAsync("/api/parcels/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

