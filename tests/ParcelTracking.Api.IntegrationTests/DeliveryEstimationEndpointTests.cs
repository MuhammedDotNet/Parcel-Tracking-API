using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Api.IntegrationTests;

public class DeliveryEstimationEndpointTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public DeliveryEstimationEndpointTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<(int ParcelId, string TrackingNumber)> SeedParcelAsync(
        string shipperCountry = "US",
        string recipientCountry = "US",
        ServiceType serviceType = ServiceType.Standard,
        ParcelStatus status = ParcelStatus.LabelCreated)
    {
        // Generate country-appropriate postal codes
        string GetPostalCode(string country) => country switch
        {
            "CA" => "K1A 0B1",
            "GB" => "SW1A 1AA",
            "FR" => "75001",
            "DE" => "10115",
            "JP" => "100-0001",
            "AU" => "2000",
            _ => "10001" // US and default
        };

        // Create shipper address
        var shipperResp = await _client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            Street1 = "1 Shipper St",
            City = "ShipCity",
            State = "SC",
            PostalCode = GetPostalCode(shipperCountry),
            CountryCode = shipperCountry,
            IsResidential = false,
            ContactName = "Shipper Corp",
            Phone = "+1-555-0100",
            Email = "ship@test.com"
        });
        
        if (!shipperResp.IsSuccessStatusCode)
        {
            var error = await shipperResp.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create shipper address: {shipperResp.StatusCode} - {error}");
        }
        
        var shipper = await shipperResp.Content.ReadFromJsonAsync<AddressResponse>();

        // Create recipient address
        var recipientResp = await _client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            Street1 = "2 Recipient Ave",
            City = "RecCity",
            State = "RC",
            PostalCode = GetPostalCode(recipientCountry),
            CountryCode = recipientCountry,
            IsResidential = true,
            ContactName = "Jane Recipient",
            Phone = "+1-555-0200",
            Email = "receive@test.com"
        });
        
        if (!recipientResp.IsSuccessStatusCode)
        {
            var error = await recipientResp.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create recipient address: {recipientResp.StatusCode} - {error}");
        }
        
        var recipient = await recipientResp.Content.ReadFromJsonAsync<AddressResponse>();

        // Create parcel
        var parcelResp = await _client.PostAsJsonAsync("/api/parcels", new RegisterParcelRequest
        {
            ShipperAddressId = shipper!.Id,
            RecipientAddressId = recipient!.Id,
            ServiceType = serviceType.ToString(),
            Description = "Test parcel",
            Weight = new WeightDto { Value = 1m, Unit = "kg" },
            Dimensions = new DimensionsDto { Length = 20, Width = 15, Height = 10, Unit = "cm" },
            DeclaredValue = new DeclaredValueDto { Amount = 100m, Currency = "USD" },
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode = "8471.30",
                    Description = "Test item",
                    Quantity = 1,
                    UnitValue = 100m,
                    Currency = "USD",
                    Weight = 1m,
                    WeightUnit = "kg",
                    CountryOfOrigin = shipperCountry
                }
            ]
        });
        
        if (!parcelResp.IsSuccessStatusCode)
        {
            var error = await parcelResp.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create parcel: {parcelResp.StatusCode} - {error}");
        }
        
        var parcel = await parcelResp.Content.ReadFromJsonAsync<ParcelResponse>();

        // Update status if needed (can't set status during creation)
        if (status != ParcelStatus.LabelCreated)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var entity = await db.Parcels.FindAsync(parcel!.Id);
            if (entity is not null)
            {
                entity.Status = status;
                await db.SaveChangesAsync();
            }
        }

        return (parcel!.Id, parcel.TrackingNumber);
    }

    // ─── Property Tests ─────────────────────────────────────────────────────

    // Feature: delivery-estimation, Property 12: GET endpoint returns 200 for existing parcels
    [Fact]
    public async Task Property_GetEndpointReturns200ForExistingParcels()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express, ServiceType.Overnight };
        var statuses = new[] { ParcelStatus.LabelCreated, ParcelStatus.PickedUp, ParcelStatus.InTransit, 
            ParcelStatus.OutForDelivery, ParcelStatus.Delivered, ParcelStatus.Exception, ParcelStatus.Returned };
        var countries = new[] { "US", "CA", "GB", "FR", "DE", "JP", "AU" };

        for (int i = 0; i < iterations; i++)
        {
            // Generate random parcel configuration
            var shipperCountry = faker.PickRandom(countries);
            var recipientCountry = faker.PickRandom(countries);
            var serviceType = faker.PickRandom(serviceTypes);
            var status = faker.PickRandom(statuses);

            // Create parcel with random configuration
            var (parcelId, _) = await SeedParcelAsync(shipperCountry, recipientCountry, serviceType, status);

            // Act: GET delivery estimate
            var response = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");

            // Assert: Should return 200 OK for any existing parcel
            response.StatusCode.Should().Be(HttpStatusCode.OK, 
                $"GET should return 200 for existing parcel with service={serviceType}, status={status}, " +
                $"shipper={shipperCountry}, recipient={recipientCountry}");

            // Verify response contains a valid delivery estimate
            var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
            estimate.Should().NotBeNull();
            estimate!.EarliestDelivery.Should().NotBe(default(DateOnly));
            estimate.LatestDelivery.Should().NotBe(default(DateOnly));
            estimate.Confidence.Should().NotBeNullOrEmpty();
            estimate.ServiceType.Should().NotBeNullOrEmpty();
        }
    }

    // Feature: delivery-estimation, Property 13: Response contains all required fields
    // Validates: Requirements 7.4, 10.1, 10.2
    [Fact]
    public async Task Property13_ResponseContainsAllRequiredFields()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express, ServiceType.Overnight };
        var statuses = new[] { ParcelStatus.LabelCreated, ParcelStatus.PickedUp, ParcelStatus.InTransit, 
            ParcelStatus.OutForDelivery, ParcelStatus.Delivered, ParcelStatus.Exception, ParcelStatus.Returned };
        var countries = new[] { "US", "CA", "GB", "FR", "DE", "JP", "AU" };

        for (int i = 0; i < iterations; i++)
        {
            // Generate random parcel configuration
            var shipperCountry = faker.PickRandom(countries);
            var recipientCountry = faker.PickRandom(countries);
            var serviceType = faker.PickRandom(serviceTypes);
            var status = faker.PickRandom(statuses);

            // Create parcel with random configuration
            var (parcelId, _) = await SeedParcelAsync(shipperCountry, recipientCountry, serviceType, status);

            // Act: GET delivery estimate
            var response = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");

            // Assert: Response should be successful
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
            estimate.Should().NotBeNull();

            // Requirement 7.4, 10.1, 10.2: Response must include all required fields
            
            // Earliest delivery date must be present and valid
            estimate!.EarliestDelivery.Should().NotBe(default(DateOnly),
                $"EarliestDelivery must be present for service={serviceType}, status={status}");
            
            // Latest delivery date must be present and valid
            estimate.LatestDelivery.Should().NotBe(default(DateOnly),
                $"LatestDelivery must be present for service={serviceType}, status={status}");
            
            // Confidence must be present and non-empty (Requirement 7.4)
            estimate.Confidence.Should().NotBeNullOrEmpty(
                $"Confidence must be present for service={serviceType}, status={status}");
            
            // Confidence should be one of the valid values
            estimate.Confidence.Should().BeOneOf("Low", "Medium", "High",
                $"Confidence must be a valid value for service={serviceType}, status={status}");
            
            // Service type must be present and non-empty (Requirement 10.1)
            estimate.ServiceType.Should().NotBeNullOrEmpty(
                $"ServiceType must be present for service={serviceType}, status={status}");
            
            // Service type should match the parcel's service type
            estimate.ServiceType.Should().Be(serviceType.ToString(),
                $"ServiceType should match the parcel's service type");
            
            // IsInternational flag must be present (Requirement 10.2)
            // This is a boolean, so it's always present, but we verify it's set correctly
            var expectedIsInternational = !shipperCountry.Equals(recipientCountry, StringComparison.OrdinalIgnoreCase);
            estimate.IsInternational.Should().Be(expectedIsInternational,
                $"IsInternational should be {expectedIsInternational} for shipper={shipperCountry}, recipient={recipientCountry}");
            
            // Verify earliest is before or equal to latest
            estimate.EarliestDelivery.Should().BeOnOrBefore(estimate.LatestDelivery,
                $"EarliestDelivery should be <= LatestDelivery for service={serviceType}, status={status}");
        }
    }

    // ─── Integration Tests ──────────────────────────────────────────────────

    // Validates: Requirements 7.3
    [Fact]
    public async Task GetEstimate_NonExistentParcel_Returns404()
    {
        // Arrange: Use a parcel ID that doesn't exist
        var nonExistentParcelId = 999999;

        // Act: GET delivery estimate for non-existent parcel
        var response = await _client.GetAsync($"/api/parcels/{nonExistentParcelId}/delivery-estimate");

        // Assert: Should return 404 Not Found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "GET should return 404 when the parcel does not exist");
    }
}
