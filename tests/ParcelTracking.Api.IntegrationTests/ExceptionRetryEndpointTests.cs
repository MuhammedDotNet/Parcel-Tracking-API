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
public class ExceptionRetryEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _authedClient;
    private readonly IntegrationTestFixture _fixture;

    public ExceptionRetryEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _authedClient = fixture.Factory.CreateClient();
        _authedClient.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<(int ParcelId, string TrackingNumber)> SeedParcelAsync(
        ParcelStatus status = ParcelStatus.InTransit,
        int deliveryAttempts = 0)
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

        // Update parcel status directly in DB if needed
        if (status != ParcelStatus.LabelCreated || deliveryAttempts > 0)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var entity = await db.Parcels.FindAsync(parcel!.Id);
            entity!.Status = status;
            entity.DeliveryAttempts = deliveryAttempts;
            await db.SaveChangesAsync();
        }

        return (parcel!.Id, parcel.TrackingNumber);
    }

    // ─── Exception Reporting Tests ─────────────────────────────────────────

    [Fact]
    public async Task ReportException_InTransitParcel_Returns200_WithExceptionStatus()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.RecipientUnavailable });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Exception");
    }

    [Fact]
    public async Task ReportException_OutForDeliveryParcel_Returns200_WithExceptionStatus()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.OutForDelivery);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.AddressNotFound });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        body!.Status.Should().Be("Exception");
    }

    [Fact]
    public async Task ReportException_DeliveredParcel_Returns400()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.Delivered);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.WeatherDelay });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Delivered");
    }

    [Fact]
    public async Task ReportException_NonExistentParcel_Returns404()
    {
        var response = await _authedClient.PostAsJsonAsync(
            "/api/parcels/99999/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.WeatherDelay });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Retry Delivery Tests ──────────────────────────────────────────────

    [Fact]
    public async Task RetryDelivery_ExceptionParcelUnderLimit_Returns200_WithInTransitStatus()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.Exception, deliveryAttempts: 1);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("InTransit");
    }

    [Fact]
    public async Task RetryDelivery_NonExceptionParcel_Returns400()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RetryDelivery_PastDate_Returns400()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.Exception, deliveryAttempts: 1);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(-2) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RetryDelivery_MaxAttempts_Returns400_WithAutoReturn()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.Exception, deliveryAttempts: 3);

        var response = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Maximum delivery attempts (3) reached");
    }

    [Fact]
    public async Task RetryDelivery_NonExistentParcel_Returns404()
    {
        var response = await _authedClient.PostAsJsonAsync(
            "/api/parcels/99999/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(5) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Exception Monitoring Tests ────────────────────────────────────────

    [Fact]
    public async Task GetExceptions_ReturnsOnlyExceptionParcels()
    {
        // Seed parcels in various statuses
        var (excId1, _) = await SeedParcelAsync(ParcelStatus.Exception, 1);
        var (excId2, _) = await SeedParcelAsync(ParcelStatus.Exception, 2);
        await SeedParcelAsync(ParcelStatus.InTransit);
        await SeedParcelAsync(ParcelStatus.Delivered);

        var response = await _authedClient.GetAsync("/api/parcels/exceptions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ParcelDetailResponse>>();
        body.Should().NotBeNull();
        body!.Should().OnlyContain(p => p.Status == "Exception");
        body.Should().Contain(p => p.Id == excId1);
        body.Should().Contain(p => p.Id == excId2);
    }

    [Fact]
    public async Task GetExceptions_NoExceptions_ReturnsEmptyArray()
    {
        // Respawn ensures clean DB state via IAsyncLifetime
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");

        var response = await client.GetAsync("/api/parcels/exceptions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ParcelDetailResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExceptions_IncludesAddressData()
    {
        await SeedParcelAsync(ParcelStatus.Exception, 1);

        var response = await _authedClient.GetAsync("/api/parcels/exceptions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ParcelDetailResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();

        var first = body.First();
        first.ShipperAddress.Should().NotBeNull();
        first.RecipientAddress.Should().NotBeNull();
        first.ShipperAddress.City.Should().NotBeNullOrEmpty();
        first.RecipientAddress.City.Should().NotBeNullOrEmpty();
    }

    // ─── Full Exception-Retry Cycle Tests ──────────────────────────────────

    [Fact]
    public async Task FullCycle_Exception_Retry_Exception_Retry_Exception_AutoReturn()
    {
        // Create a parcel in InTransit
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        // Exception 1
        var ex1 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.RecipientUnavailable });
        ex1.StatusCode.Should().Be(HttpStatusCode.OK);
        var ex1Body = await ex1.Content.ReadFromJsonAsync<ParcelResponse>();
        ex1Body!.Status.Should().Be("Exception");

        // Retry 1
        var retry1 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(2) });
        retry1.StatusCode.Should().Be(HttpStatusCode.OK);
        var retry1Body = await retry1.Content.ReadFromJsonAsync<ParcelResponse>();
        retry1Body!.Status.Should().Be("InTransit");

        // Exception 2
        var ex2 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.AddressNotFound });
        ex2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Retry 2
        var retry2 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3) });
        retry2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Exception 3
        var ex3 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.WeatherDelay });
        ex3.StatusCode.Should().Be(HttpStatusCode.OK);

        // Retry 3 → should auto-return with 400
        var retry3 = await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(4) });
        retry3.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await retry3.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("Maximum delivery attempts (3) reached");
        problem.Detail.Should().Contain("returned to sender");

        // Verify parcel is actually in Returned status
        var verifyResp = await _authedClient.GetAsync($"/api/parcels/{parcelId}");
        verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyBody = await verifyResp.Content.ReadFromJsonAsync<ParcelDetailResponse>();
        verifyBody!.Status.Should().Be("Returned");
    }

    [Fact]
    public async Task TrackingHistory_ReflectsExceptionRetryEvents()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        // Exception
        await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/exception",
            new ReportExceptionRequest { Reason = ExceptionReason.DamagedPackage });

        // Retry
        await _authedClient.PostAsJsonAsync(
            $"/api/parcels/{parcelId}/retry",
            new RetryDeliveryRequest { NewEstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3) });

        // Get tracking history
        var response = await _authedClient.GetAsync($"/api/parcels/{parcelId}/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await response.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
        events.Should().NotBeNull();

        // Should have at least the exception and retry tracking events
        events!.Should().Contain(e => e.EventType == "DeliveryAttempted");
        events.Should().Contain(e => e.EventType == "InTransit");
    }

    [Fact]
    public async Task InvalidEnumValue_Returns400()
    {
        var (parcelId, _) = await SeedParcelAsync(ParcelStatus.InTransit);

        // Send invalid enum value as raw JSON
        var content = new StringContent(
            """{"reason": "InvalidReason"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _authedClient.PostAsync(
            $"/api/parcels/{parcelId}/exception", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
