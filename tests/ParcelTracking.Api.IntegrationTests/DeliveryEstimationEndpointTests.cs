using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Infrastructure.Data;

namespace ParcelTracking.Api.IntegrationTests;

[Collection("Database")]
public class DeliveryEstimationEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFixture _fixture;

    public DeliveryEstimationEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
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
            using var scope = _fixture.Factory.Services.CreateScope();
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

    // Validates: Requirements 7.1, 7.2, 7.4
    [Fact]
    public async Task GetEstimate_DomesticParcel_ReturnsDeliveryWindow()
    {
        // Arrange: Create a domestic parcel (same country codes)
        var (parcelId, trackingNumber) = await SeedParcelAsync(
            shipperCountry: "US",
            recipientCountry: "US",
            serviceType: ServiceType.Standard,
            status: ParcelStatus.InTransit);

        // Act: GET delivery estimate
        var response = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");

        // Assert: Should return 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET should return 200 OK for existing domestic parcel");

        // Verify response contains valid delivery estimate
        var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
        estimate.Should().NotBeNull();

        // Requirement 7.4: Response should include all required fields
        estimate!.EarliestDelivery.Should().NotBe(default);
        estimate.LatestDelivery.Should().NotBe(default);
        estimate.Confidence.Should().NotBeNullOrEmpty();
        estimate.ServiceType.Should().Be("Standard");
        
        // Verify it's classified as domestic
        estimate.IsInternational.Should().BeFalse(
            "Parcel with same country codes should be classified as domestic");

        // Verify earliest is before or equal to latest
        estimate.EarliestDelivery.Should().BeOnOrBefore(estimate.LatestDelivery,
            "EarliestDelivery should be on or before LatestDelivery");

        // Verify confidence matches status (InTransit → Medium)
        estimate.Confidence.Should().Be("Medium",
            "InTransit status should result in Medium confidence");

        // Verify JSON serialization format
        var jsonContent = await response.Content.ReadAsStringAsync();
        jsonContent.Should().Contain("\"earliestDelivery\"",
            "JSON should use camelCase property names");
        jsonContent.Should().Contain("\"latestDelivery\"",
            "JSON should use camelCase property names");
    }

    // Validates: Requirements 7.1, 7.2, 7.4, 10.2
    [Fact]
    public async Task GetEstimate_InternationalParcel_ReturnsLongerDeliveryWindow()
    {
        // Arrange: Create an international parcel (different country codes)
        var (parcelId, trackingNumber) = await SeedParcelAsync(
            shipperCountry: "US",
            recipientCountry: "CA",
            serviceType: ServiceType.Standard,
            status: ParcelStatus.PickedUp);

        // Act: GET delivery estimate
        var response = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");

        // Assert: Should return 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET should return 200 OK for existing international parcel");

        // Verify response contains valid delivery estimate
        var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
        estimate.Should().NotBeNull();

        // Requirement 7.4, 10.2: Response should include all required fields
        estimate!.EarliestDelivery.Should().NotBe(default);
        estimate.LatestDelivery.Should().NotBe(default);
        estimate.Confidence.Should().NotBeNullOrEmpty();
        estimate.ServiceType.Should().Be("Standard");
        
        // Requirement 10.2: Verify it's classified as international
        estimate.IsInternational.Should().BeTrue(
            "Parcel with different country codes should be classified as international");

        // Verify earliest is before or equal to latest
        estimate.EarliestDelivery.Should().BeOnOrBefore(estimate.LatestDelivery,
            "EarliestDelivery should be on or before LatestDelivery");

        // Verify confidence matches status (PickedUp → Low)
        estimate.Confidence.Should().Be("Low",
            "PickedUp status should result in Low confidence");

        // Verify the delivery window is longer than domestic
        // For Standard service: domestic is 3-5 days, international adds 3-5 days = 6-10 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowSize = estimate.LatestDelivery.DayNumber - estimate.EarliestDelivery.DayNumber;
        
        // International Standard should have a window of at least 4 days (10 - 6 = 4)
        windowSize.Should().BeGreaterThanOrEqualTo(4,
            "International parcels should have a longer delivery window than domestic");
    }

    // Validates: Requirements 8.1, 8.5
    [Fact]
    public async Task PutRecalculate_AfterExceptionEvent_UpdatesEstimate()
    {
        // Arrange: Create a parcel with Exception status
        var (parcelId, trackingNumber) = await SeedParcelAsync(
            shipperCountry: "US",
            recipientCountry: "US",
            serviceType: ServiceType.Express,
            status: ParcelStatus.Exception);

        // Add a tracking event to simulate an exception
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var parcel = await db.Parcels
                .Include(p => p.TrackingEvents)
                .FirstOrDefaultAsync(p => p.Id == parcelId);
            
            parcel.Should().NotBeNull();
            
            // Add an exception event
            parcel!.TrackingEvents.Add(new Domain.Entities.TrackingEvent
            {
                ParcelId = parcelId,
                EventType = EventType.Exception,
                Timestamp = DateTime.UtcNow.AddHours(-2),
                Description = "Delivery exception - address issue",
                LocationCity = "Distribution Center",
                LocationState = "CA",
                LocationCountry = "US"
            });
            
            await db.SaveChangesAsync();
        }

        // Get the initial estimate
        var initialResponse = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialEstimate = await initialResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();

        // Act: PUT recalculate after exception
        var recalcResponse = await _client.PutAsync($"/api/parcels/{parcelId}/delivery-estimate/recalculate", null);

        // Assert: Should return 200 OK
        recalcResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "PUT recalculate should return 200 OK");

        // Requirement 8.5: Verify response contains updated delivery estimate
        var recalcEstimate = await recalcResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
        recalcEstimate.Should().NotBeNull();
        recalcEstimate!.EarliestDelivery.Should().NotBe(default);
        recalcEstimate.LatestDelivery.Should().NotBe(default);
        recalcEstimate.Confidence.Should().Be("Low",
            "Exception status should result in Low confidence");
        recalcEstimate.ServiceType.Should().Be("Express");

        // Verify the estimate was persisted by fetching it again
        var verifyResponse = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifiedEstimate = await verifyResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();

        // The persisted estimate should match the recalculated estimate
        verifiedEstimate!.LatestDelivery.Should().Be(recalcEstimate.LatestDelivery,
            "Persisted latest delivery should match recalculated value");

        // Verify JSON serialization format (ISO 8601 dates, camelCase)
        var jsonContent = await recalcResponse.Content.ReadAsStringAsync();
        jsonContent.Should().Contain("\"earliestDelivery\"",
            "JSON should use camelCase property names");
        jsonContent.Should().MatchRegex(@"""earliestDelivery""\s*:\s*""\d{4}-\d{2}-\d{2}""",
            "earliestDelivery should be in ISO 8601 format (YYYY-MM-DD)");
        jsonContent.Should().MatchRegex(@"""latestDelivery""\s*:\s*""\d{4}-\d{2}-\d{2}""",
            "latestDelivery should be in ISO 8601 format (YYYY-MM-DD)");
    }

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

    // Feature: delivery-estimation, Property 14: Recalculation persists and returns estimate
    // Validates: Requirements 8.5
    [Fact]
    public async Task Property14_RecalculationPersistsAndReturnsEstimate()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express, ServiceType.Overnight };
        var statuses = new[] { ParcelStatus.LabelCreated, ParcelStatus.PickedUp, ParcelStatus.InTransit, 
            ParcelStatus.OutForDelivery, ParcelStatus.Exception };
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

            // Get the initial estimate to compare
            var initialResponse = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");
            initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var initialEstimate = await initialResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();

            // Act: PUT recalculate
            var recalcResponse = await _client.PutAsync($"/api/parcels/{parcelId}/delivery-estimate/recalculate", null);

            // Assert: Should return 200 OK
            recalcResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"PUT recalculate should return 200 for existing parcel with service={serviceType}, status={status}, " +
                $"shipper={shipperCountry}, recipient={recipientCountry}");

            // Verify response contains a valid delivery estimate
            var recalcEstimate = await recalcResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
            recalcEstimate.Should().NotBeNull();
            recalcEstimate!.EarliestDelivery.Should().NotBe(default(DateOnly));
            recalcEstimate.LatestDelivery.Should().NotBe(default(DateOnly));
            recalcEstimate.Confidence.Should().NotBeNullOrEmpty();
            recalcEstimate.ServiceType.Should().NotBeNullOrEmpty();

            // Requirement 8.5: Verify the estimate was persisted by fetching it again
            var verifyResponse = await _client.GetAsync($"/api/parcels/{parcelId}/delivery-estimate");
            verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var verifiedEstimate = await verifyResponse.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();

            // The persisted estimate should match the recalculated estimate
            verifiedEstimate!.EarliestDelivery.Should().Be(recalcEstimate.EarliestDelivery,
                "Persisted earliest delivery should match recalculated value");
            verifiedEstimate.LatestDelivery.Should().Be(recalcEstimate.LatestDelivery,
                "Persisted latest delivery should match recalculated value");
            verifiedEstimate.Confidence.Should().Be(recalcEstimate.Confidence,
                "Persisted confidence should match recalculated value");

            // Verify that the database was actually updated by checking the parcel directly
            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var parcel = await db.Parcels.FindAsync(parcelId);
            
            parcel.Should().NotBeNull();
            
            // Requirement 6.5: The EstimatedDeliveryDate should be updated to the latest delivery date
            var expectedStoredDate = new DateTimeOffset(
                recalcEstimate.LatestDelivery.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            
            parcel!.EstimatedDeliveryDate.Should().Be(expectedStoredDate,
                $"EstimatedDeliveryDate should be updated to the latest delivery date for service={serviceType}, status={status}");
            
            // Verify UpdatedAt was updated (should be very recent)
            parcel.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5),
                "UpdatedAt should be updated to current time");
        }
    }

    // Feature: delivery-estimation, Property 15: ISO 8601 date serialization
    // Validates: Requirements 10.3
    [Fact]
    public async Task Property15_ISO8601DateSerialization()
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

            // Assert: Should return 200 OK
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get the raw JSON response
            var jsonContent = await response.Content.ReadAsStringAsync();
            jsonContent.Should().NotBeNullOrEmpty();

            // Parse the JSON to verify date format
            var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Requirement 10.3: Dates should be in ISO 8601 format (YYYY-MM-DD)
            root.TryGetProperty("earliestDelivery", out var earliestDeliveryElement).Should().BeTrue(
                "Response should contain earliestDelivery property");
            root.TryGetProperty("latestDelivery", out var latestDeliveryElement).Should().BeTrue(
                "Response should contain latestDelivery property");

            var earliestDeliveryStr = earliestDeliveryElement.GetString();
            var latestDeliveryStr = latestDeliveryElement.GetString();

            earliestDeliveryStr.Should().NotBeNullOrEmpty();
            latestDeliveryStr.Should().NotBeNullOrEmpty();

            // Verify ISO 8601 date format (YYYY-MM-DD)
            // The format should be exactly 10 characters: YYYY-MM-DD
            earliestDeliveryStr!.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$",
                $"earliestDelivery should be in ISO 8601 format (YYYY-MM-DD), got: {earliestDeliveryStr}");
            latestDeliveryStr!.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$",
                $"latestDelivery should be in ISO 8601 format (YYYY-MM-DD), got: {latestDeliveryStr}");

            // Verify the dates can be parsed as valid DateOnly values
            DateOnly.TryParse(earliestDeliveryStr, out var earliestDate).Should().BeTrue(
                $"earliestDelivery should be a valid date: {earliestDeliveryStr}");
            DateOnly.TryParse(latestDeliveryStr, out var latestDate).Should().BeTrue(
                $"latestDelivery should be a valid date: {latestDeliveryStr}");

            // Verify the parsed dates are valid (earliest <= latest)
            earliestDate.Should().BeOnOrBefore(latestDate,
                $"earliestDelivery ({earliestDate}) should be on or before latestDelivery ({latestDate})");
        }
    }

    // Feature: delivery-estimation, Property 16: Confidence serialized as string
    // Validates: Requirements 2.4, 10.4
    [Fact]
    public async Task Property16_ConfidenceSerializedAsString()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express, ServiceType.Overnight };
        var statuses = new[] { ParcelStatus.LabelCreated, ParcelStatus.PickedUp, ParcelStatus.InTransit, 
            ParcelStatus.OutForDelivery, ParcelStatus.Delivered, ParcelStatus.Exception, ParcelStatus.Returned };
        var countries = new[] { "US", "CA", "GB", "FR", "DE", "JP", "AU" };

        // Define expected confidence mapping according to requirements 2.1, 2.2, 2.3
        var expectedConfidenceMapping = new Dictionary<ParcelStatus, string>
        {
            [ParcelStatus.OutForDelivery] = "High",
            [ParcelStatus.Delivered] = "High",
            [ParcelStatus.InTransit] = "Medium",
            [ParcelStatus.LabelCreated] = "Low",
            [ParcelStatus.PickedUp] = "Low",
            [ParcelStatus.Exception] = "Low",
            [ParcelStatus.Returned] = "Low"
        };

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

            // Assert: Should return 200 OK
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get the raw JSON response
            var jsonContent = await response.Content.ReadAsStringAsync();
            jsonContent.Should().NotBeNullOrEmpty();

            // Parse the JSON to verify confidence is a string
            var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Requirement 2.4, 10.4: Confidence should be serialized as a string
            root.TryGetProperty("confidence", out var confidenceElement).Should().BeTrue(
                "Response should contain confidence property");

            // Verify it's a string type in JSON
            confidenceElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.String,
                "Confidence should be serialized as a JSON string");

            var confidenceStr = confidenceElement.GetString();
            confidenceStr.Should().NotBeNullOrEmpty();

            // Verify the confidence value is one of the valid string values
            confidenceStr.Should().BeOneOf("Low", "Medium", "High",
                $"Confidence should be one of the valid string values, got: {confidenceStr}");

            // Verify the confidence matches the expected value for the parcel status
            var expectedConfidence = expectedConfidenceMapping[status];
            confidenceStr.Should().Be(expectedConfidence,
                $"Confidence should be '{expectedConfidence}' for status {status}, got: {confidenceStr}");

            // Also verify using the deserialized DTO
            var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
            estimate.Should().NotBeNull();
            estimate!.Confidence.Should().Be(expectedConfidence,
                $"Deserialized confidence should match expected value for status {status}");
        }
    }

    // Feature: delivery-estimation, Property 17: CamelCase JSON properties
    // Validates: Requirements 10.5
    [Fact]
    public async Task Property17_CamelCaseJsonProperties()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        var serviceTypes = new[] { ServiceType.Economy, ServiceType.Standard, ServiceType.Express, ServiceType.Overnight };
        var statuses = new[] { ParcelStatus.LabelCreated, ParcelStatus.PickedUp, ParcelStatus.InTransit, 
            ParcelStatus.OutForDelivery, ParcelStatus.Delivered, ParcelStatus.Exception, ParcelStatus.Returned };
        var countries = new[] { "US", "CA", "GB", "FR", "DE", "JP", "AU" };

        // Define the expected camelCase property names
        var expectedProperties = new[]
        {
            "earliestDelivery",
            "latestDelivery",
            "confidence",
            "serviceType",
            "isInternational"
        };

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

            // Assert: Should return 200 OK
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Get the raw JSON response
            var jsonContent = await response.Content.ReadAsStringAsync();
            jsonContent.Should().NotBeNullOrEmpty();

            // Parse the JSON to verify property names
            var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Requirement 10.5: All property names should use camelCase
            foreach (var expectedProperty in expectedProperties)
            {
                root.TryGetProperty(expectedProperty, out _).Should().BeTrue(
                    $"Response should contain camelCase property '{expectedProperty}'");
            }

            // Verify that PascalCase versions do NOT exist (to ensure camelCase is used)
            var pascalCaseProperties = new[]
            {
                "EarliestDelivery",
                "LatestDelivery",
                "Confidence",
                "ServiceType",
                "IsInternational"
            };

            foreach (var pascalProperty in pascalCaseProperties)
            {
                root.TryGetProperty(pascalProperty, out _).Should().BeFalse(
                    $"Response should NOT contain PascalCase property '{pascalProperty}' - camelCase should be used instead");
            }

            // Verify each property name starts with a lowercase letter
            foreach (var property in root.EnumerateObject())
            {
                var propertyName = property.Name;
                propertyName.Should().NotBeNullOrEmpty();
                
                // First character should be lowercase (camelCase convention)
                char.IsLower(propertyName[0]).Should().BeTrue(
                    $"Property name '{propertyName}' should start with a lowercase letter (camelCase)");
            }

            // Verify the specific expected properties match the camelCase pattern
            root.TryGetProperty("earliestDelivery", out _).Should().BeTrue(
                "earliestDelivery should be in camelCase (not EarliestDelivery)");
            root.TryGetProperty("latestDelivery", out _).Should().BeTrue(
                "latestDelivery should be in camelCase (not LatestDelivery)");
            root.TryGetProperty("confidence", out _).Should().BeTrue(
                "confidence should be in camelCase (not Confidence)");
            root.TryGetProperty("serviceType", out _).Should().BeTrue(
                "serviceType should be in camelCase (not ServiceType)");
            root.TryGetProperty("isInternational", out _).Should().BeTrue(
                "isInternational should be in camelCase (not IsInternational)");
        }
    }

    // Validates: Requirements 8.4
    [Fact]
    public async Task Recalculate_NoTrackingEvents_UsesCurrentDate()
    {
        // Arrange: Create a parcel
        var (parcelId, _) = await SeedParcelAsync(
            shipperCountry: "US",
            recipientCountry: "US",
            serviceType: ServiceType.Standard,
            status: ParcelStatus.LabelCreated);

        // Remove any tracking events to test the fallback behavior
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var parcel = await db.Parcels
                .Include(p => p.TrackingEvents)
                .FirstOrDefaultAsync(p => p.Id == parcelId);
            
            parcel.Should().NotBeNull();
            
            // Remove all tracking events to test the fallback to current date
            parcel!.TrackingEvents.Clear();
            await db.SaveChangesAsync();
        }

        // Verify the parcel now has no tracking events
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var parcel = await db.Parcels
                .Include(p => p.TrackingEvents)
                .FirstOrDefaultAsync(p => p.Id == parcelId);
            
            parcel.Should().NotBeNull();
            parcel!.TrackingEvents.Should().BeEmpty("Test setup requires no tracking events");
        }

        // Act: PUT recalculate (should use current date as fallback)
        var beforeRecalc = DateTimeOffset.UtcNow;
        var response = await _client.PutAsync($"/api/parcels/{parcelId}/delivery-estimate/recalculate", null);
        var afterRecalc = DateTimeOffset.UtcNow;

        // Assert: Should return 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Recalculation should succeed even when no tracking events exist");

        // Verify response contains a valid delivery estimate
        var estimate = await response.Content.ReadFromJsonAsync<DeliveryEstimateResponse>();
        estimate.Should().NotBeNull();
        estimate!.EarliestDelivery.Should().NotBe(default(DateOnly));
        estimate.LatestDelivery.Should().NotBe(default(DateOnly));

        // Requirement 8.4: When no tracking events exist, the system should use current date
        // The earliest delivery should be after today (since we're adding business days from today)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        estimate.EarliestDelivery.Should().BeOnOrAfter(today,
            "When no tracking events exist, recalculation should use current date as starting point");

        // Verify the database was updated
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParcelTrackingDbContext>();
            var parcel = await db.Parcels.FindAsync(parcelId);
            
            parcel.Should().NotBeNull();
            
            // The EstimatedDeliveryDate should be updated
            var expectedStoredDate = new DateTimeOffset(
                estimate.LatestDelivery.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            
            parcel!.EstimatedDeliveryDate.Should().Be(expectedStoredDate,
                "EstimatedDeliveryDate should be updated even when no tracking events exist");
            
            // Verify UpdatedAt was updated (should be between before and after recalc)
            parcel.UpdatedAt.Should().BeOnOrAfter(beforeRecalc,
                "UpdatedAt should be updated to a time after the recalculation started");
            parcel.UpdatedAt.Should().BeOnOrBefore(afterRecalc.AddSeconds(1),
                "UpdatedAt should be updated to a time before the recalculation completed");
        }
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
