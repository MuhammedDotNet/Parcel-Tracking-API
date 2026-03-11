using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Entities;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Api.IntegrationTests;

[Collection("Database")]
public class DeliveryConfirmationEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _authedClient;
    private readonly IntegrationTestFixture _fixture;

    public DeliveryConfirmationEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _authedClient = fixture.Factory.CreateClient();
        _authedClient.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<(string TrackingNumber, int ParcelId)> SeedParcelAsync(
        ParcelStatus status = ParcelStatus.InTransit,
        DateTimeOffset? estimatedDelivery = null)
    {
        // Create shipper address
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

        // Create recipient address
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

        // Create parcel
        var parcelResp = await _authedClient.PostAsJsonAsync("/api/parcels", new RegisterParcelRequest
        {
            ShipperAddressId = shipper!.Id,
            RecipientAddressId = recipient!.Id,
            ServiceType = "Express",
            Description = "Test parcel",
            Weight = new WeightDto { Value = 1m, Unit = "kg" },
            Dimensions = new DimensionsDto { Length = 20, Width = 15, Height = 10, Unit = "cm" },
            DeclaredValue = new DeclaredValueDto { Amount = 100m, Currency = "USD" },
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode = "0000.00",
                    Description = "Test item",
                    Quantity = 1,
                    UnitValue = 100m,
                    Currency = "USD",
                    Weight = 1m,
                    WeightUnit = "kg",
                    CountryOfOrigin = "US"
                }
            ]
        });
        parcelResp.EnsureSuccessStatusCode();
        var parcel = await parcelResp.Content.ReadFromJsonAsync<ParcelResponse>();
        var trackingNumber = parcel!.TrackingNumber;

        // Update parcel status if needed
        if (status != ParcelStatus.LabelCreated || estimatedDelivery is not null)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var entity = await db.Parcels.FindAsync(parcel.Id);
            entity!.Status = status;
            entity.EstimatedDeliveryDate = estimatedDelivery;
            await db.SaveChangesAsync();
        }

        return (trackingNumber, parcel.Id);
    }

    private async Task SeedConfirmationDirectlyAsync(int parcelId, DateTimeOffset deliveredAt)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
        db.DeliveryConfirmations.Add(new DeliveryConfirmation
        {
            ParcelId = parcelId,
            ReceivedBy = "Pre-existing",
            DeliveryLocation = "Pre-existing",
            DeliveredAt = deliveredAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var parcel = await db.Parcels.FindAsync(parcelId);
        parcel!.Status = ParcelStatus.Delivered;
        await db.SaveChangesAsync();
    }

    private ConfirmDeliveryRequest CreateValidConfirmation() => new()
    {
        ReceivedBy = "John Doe",
        DeliveryLocation = "Front door",
        DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
    };

    // ─── 7.2 POST happy path ─────────────────────────────────────────────

    [Fact]
    public async Task Post_HappyPath_Returns201WithConfirmationData()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);
        var request = CreateValidConfirmation();

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<DeliveryConfirmationResponse>();
        body.Should().NotBeNull();
        body!.TrackingNumber.Should().Be(trackingNumber);
        body.ReceivedBy.Should().Be(request.ReceivedBy);
        body.DeliveryLocation.Should().Be(request.DeliveryLocation);
        body.HasSignature.Should().BeFalse();
        body.DeliveredAt.Should().BeCloseTo(request.DeliveredAt, TimeSpan.FromSeconds(1));
        body.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));

        // Verify Location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(trackingNumber);
    }

    // ─── 7.3 POST with OutForDelivery status ─────────────────────────────

    [Fact]
    public async Task Post_OutForDeliveryStatus_Returns201()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.OutForDelivery);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ─── 7.4 POST with invalid status ────────────────────────────────────

    [Fact]
    public async Task Post_LabelCreatedStatus_Returns400()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.LabelCreated);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("LabelCreated");
        problem.Detail.Should().Contain("InTransit");
        problem.Detail.Should().Contain("OutForDelivery");
    }

    // ─── 7.5 POST with Delivered status ──────────────────────────────────

    [Fact]
    public async Task Post_DeliveredStatus_Returns409()
    {
        var (trackingNumber, parcelId) = await SeedParcelAsync(ParcelStatus.InTransit);
        await SeedConfirmationDirectlyAsync(parcelId, DateTimeOffset.UtcNow.AddHours(-2));

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── 7.6 POST with duplicate confirmation ────────────────────────────

    [Fact]
    public async Task Post_DuplicateConfirmation_Returns409WithTrackingNumber()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        // First confirmation
        var resp1 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second confirmation attempt
        var resp2 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await resp2.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Extensions.Should().ContainKey("trackingNumber");
    }

    // ─── 7.7 POST with future date ──────────────────────────────────────

    [Fact]
    public async Task Post_FutureDeliveredAt_Returns400()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "John",
            DeliveryLocation = "Door",
            DeliveredAt = DateTimeOffset.UtcNow.AddDays(5)
        };

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 7.8 POST with missing required fields ──────────────────────────

    [Fact]
    public async Task Post_MissingReceivedBy_Returns400()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "",
            DeliveryLocation = "Door",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_MissingDeliveryLocation_Returns400()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "John",
            DeliveryLocation = "",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 7.9 POST with invalid base64 ───────────────────────────────────

    [Fact]
    public async Task Post_InvalidBase64_Returns400()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "John",
            DeliveryLocation = "Door",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1),
            SignatureImage = "not-valid-base64!!"
        };

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 7.10 GET happy path ─────────────────────────────────────────────

    [Fact]
    public async Task Get_HappyPath_Returns200WithAllFields()
    {
        var estimatedDate = DateTimeOffset.UtcNow.AddDays(5);
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit, estimatedDate);
        var signature = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Jane Doe",
            DeliveryLocation = "Back door",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1),
            SignatureImage = signature
        };

        // Create confirmation first
        var postResp = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);
        postResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Retrieve confirmation
        var getResp = await _authedClient.GetAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getResp.Content.ReadFromJsonAsync<DeliveryConfirmationDetailResponse>();
        detail.Should().NotBeNull();
        detail!.TrackingNumber.Should().Be(trackingNumber);
        detail.ReceivedBy.Should().Be("Jane Doe");
        detail.DeliveryLocation.Should().Be("Back door");
        detail.SignatureImage.Should().Be(signature);
        detail.IsOnTime.Should().BeTrue(); // delivered before estimated
        detail.EstimatedDeliveryDate.Should().NotBeNull();
    }

    // ─── 7.11 GET with missing parcel ────────────────────────────────────

    [Fact]
    public async Task Get_MissingParcel_Returns404()
    {
        var response = await _authedClient.GetAsync(
            "/api/parcels/PKG-NONEXISTENT-999/delivery-confirmation");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("PKG-NONEXISTENT-999");
    }

    // ─── 7.12 GET with missing confirmation ──────────────────────────────

    [Fact]
    public async Task Get_MissingConfirmation_Returns404()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        var response = await _authedClient.GetAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain(trackingNumber);
    }

    // ─── 7.13 GET with late delivery ─────────────────────────────────────

    [Fact]
    public async Task Get_LateDelivery_IsOnTimeFalse()
    {
        // Set estimated delivery date in the past
        var estimatedDate = DateTimeOffset.UtcNow.AddDays(-5);
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit, estimatedDate);

        // Deliver today (after estimated date)
        var request = new ConfirmDeliveryRequest
        {
            ReceivedBy = "Late Receiver",
            DeliveryLocation = "Lobby",
            DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", request);

        var getResp = await _authedClient.GetAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getResp.Content.ReadFromJsonAsync<DeliveryConfirmationDetailResponse>();
        detail!.IsOnTime.Should().BeFalse();
    }

    // ─── 7.14 GET with no estimated date ─────────────────────────────────

    [Fact]
    public async Task Get_NoEstimatedDate_IsOnTimeFalse()
    {
        var (trackingNumber, _) = await SeedParcelAsync(ParcelStatus.InTransit, estimatedDelivery: null);

        await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation", CreateValidConfirmation());

        var getResp = await _authedClient.GetAsync(
            $"/api/parcels/{trackingNumber}/delivery-confirmation");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getResp.Content.ReadFromJsonAsync<DeliveryConfirmationDetailResponse>();
        detail!.IsOnTime.Should().BeFalse();
    }

    // ─── POST with non-existent tracking number ─────────────────────────

    [Fact]
    public async Task Post_NonExistentTrackingNumber_Returns404()
    {
        var response = await _authedClient.PostAsJsonAsync(
            "/api/parcels/PKG-NONEXISTENT-999/delivery-confirmation", CreateValidConfirmation());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("PKG-NONEXISTENT-999");
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
