using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests;

[Collection("Database")]
public class TrackingEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _authedClient;
    private readonly HttpClient _anonClient;

    public TrackingEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _authedClient = fixture.Factory.CreateClient();
        _authedClient.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");

        _anonClient = fixture.Factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<string> SeedParcelAndGetTrackingNumberAsync()
    {
        var shipperResp = await _authedClient.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
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

        var recipientResp = await _authedClient.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
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

        var parcelResp = await _authedClient.PostAsJsonAsync("/api/parcels", new RegisterParcelRequest
        {
            ShipperAddressId = shipper!.Id,
            RecipientAddressId = recipient!.Id,
            ServiceType = "Express",
            Description = "Electronics - Laptop",
            Weight = new WeightDto { Value = 2.5m, Unit = "kg" },
            Dimensions = new DimensionsDto { Length = 40, Width = 30, Height = 10, Unit = "cm" },
            DeclaredValue = new DeclaredValueDto { Amount = 1200m, Currency = "USD" },
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode = "8471.30",
                    Description = "Portable laptop computer",
                    Quantity = 1,
                    UnitValue = 1200m,
                    Currency = "USD",
                    Weight = 2.1m,
                    WeightUnit = "kg",
                    CountryOfOrigin = "CN"
                }
            ]
        });
        parcelResp.EnsureSuccessStatusCode();
        var parcel = await parcelResp.Content.ReadFromJsonAsync<ParcelResponse>();
        return parcel!.TrackingNumber;
    }

    // ─── GET /api/tracking/{trackingNumber} ─────────────────────────────

    [Fact]
    public async Task GetByTrackingNumber_WhenExists_Returns200WithCuratedData()
    {
        var trackingNumber = await SeedParcelAndGetTrackingNumberAsync();

        var response = await _anonClient.GetAsync($"/api/tracking/{trackingNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tracking = await response.Content.ReadFromJsonAsync<TrackingResponse>();
        tracking.Should().NotBeNull();
        tracking!.TrackingNumber.Should().Be(trackingNumber);
        tracking.Status.Should().Be("LabelCreated");
        tracking.RecipientCity.Should().Be("RecCity");
        tracking.RecipientState.Should().Be("RC");
        tracking.IsDelivered.Should().BeFalse();
    }

    [Fact]
    public async Task GetByTrackingNumber_WhenNotExists_Returns404ProblemDetails()
    {
        var response = await _anonClient.GetAsync("/api/tracking/PKG-NONEXISTENT-000000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Tracking Number Not Found");
        problem.Status.Should().Be(404);
        problem.Detail.Should().Contain("PKG-NONEXISTENT-000000");
    }

    [Fact]
    public async Task GetByTrackingNumber_WithoutApiKey_Returns200()
    {
        var trackingNumber = await SeedParcelAndGetTrackingNumberAsync();

        // Use anonymous client — no API key
        var response = await _anonClient.GetAsync($"/api/tracking/{trackingNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private readonly IntegrationTestFixture _fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
