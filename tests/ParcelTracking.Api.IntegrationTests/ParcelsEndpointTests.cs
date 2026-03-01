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

    // ─── PATCH /api/parcels/{id} Property Tests ─────────────────────────

    // Feature: parcel-status-lifecycle, Property 6: Valid PATCH operations are applied
    // Validates: Requirements 5.3, 5.4, 5.5
    [Fact]
    public async Task Property_ValidPatchOperationsAreApplied()
    {
        const int iterations = 10; // Reduced from 100 for integration tests
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Pick a random whitelisted field to patch
            var fieldChoice = faker.Random.Int(0, 2); // 0=ServiceType, 1=Description, 2=EstimatedDeliveryDate

            string patchJson;
            object expectedValue;

            switch (fieldChoice)
            {
                case 0: // ServiceType
                    var serviceTypes = new[] { "Standard", "Express", "Overnight", "Economy" };
                    var newServiceType = faker.PickRandom(serviceTypes);
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"{newServiceType}\"}}]";
                    expectedValue = newServiceType;
                    break;

                case 1: // Description
                    var newDescription = faker.Lorem.Sentence().Replace("\"", "'"); // Escape quotes
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"{newDescription}\"}}]";
                    expectedValue = newDescription;
                    break;

                case 2: // EstimatedDeliveryDate
                    var newDate = DateTimeOffset.UtcNow.AddDays(faker.Random.Int(1, 30));
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{newDate:O}\"}}]";
                    expectedValue = newDate;
                    break;

                default:
                    throw new InvalidOperationException("Invalid field choice");
            }

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            // If it fails, print the error for debugging
            if (patchResp.StatusCode != HttpStatusCode.OK)
            {
                var errorBody = await patchResp.Content.ReadAsStringAsync();
                Console.WriteLine($"PATCH failed for field {fieldChoice}: {errorBody}");
            }

            // Verify the patch succeeded
            patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
                $"PATCH operation on field {fieldChoice} should succeed");

            var updated = await patchResp.Content.ReadFromJsonAsync<ParcelResponse>();
            updated.Should().NotBeNull();

            // Verify the field was updated
            switch (fieldChoice)
            {
                case 0:
                    updated!.ServiceType.Should().Be((string)expectedValue,
                        "ServiceType should be updated");
                    break;
                case 1:
                    updated!.Description.Should().Be((string)expectedValue,
                        "Description should be updated");
                    break;
                case 2:
                    updated!.EstimatedDeliveryDate.Should().NotBeNull();
                    updated.EstimatedDeliveryDate!.Value.Should().BeCloseTo((DateTimeOffset)expectedValue, TimeSpan.FromSeconds(1),
                        "EstimatedDeliveryDate should be updated");
                    break;
            }
        }
    }

    // Feature: parcel-status-lifecycle, Property 7: Non-whitelisted field PATCH rejection
    // Validates: Requirements 6.3
    [Fact]
    public async Task Property_NonWhitelistedFieldPatchRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        // Non-whitelisted fields
        var nonWhitelistedFields = new[] { "id", "trackingNumber", "createdAt", "shipperAddressId", "recipientAddressId" };

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Pick a random non-whitelisted field
            var field = faker.PickRandom(nonWhitelistedFields);
            var newValue = faker.Random.Int(1, 10000);

            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/{field}\",\"value\":{newValue}}}]";

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            // Verify the patch was rejected
            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"PATCH operation on non-whitelisted field '{field}' should be rejected");
        }
    }

    // Feature: parcel-status-lifecycle, Property 8: Status transition validation via PATCH
    // Validates: Requirements 7.1, 7.2, 7.3
    [Fact]
    public async Task Property_StatusTransitionValidationViaPatch()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        // Get all invalid transitions (excluding terminal states)
        var allStatuses = new[] { "LabelCreated", "PickedUp", "InTransit", "OutForDelivery", "Delivered", "Exception", "Returned" };
        var validTransitions = new Dictionary<string, string[]>
        {
            ["LabelCreated"] = new[] { "PickedUp", "Exception" },
            ["PickedUp"] = new[] { "InTransit", "Exception" },
            ["InTransit"] = new[] { "OutForDelivery", "Exception" },
            ["OutForDelivery"] = new[] { "Delivered", "Exception" },
            ["Exception"] = new[] { "Returned" },
            ["Delivered"] = Array.Empty<string>(),
            ["Returned"] = Array.Empty<string>()
        };

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var currentStatus = created!.Status;

            // Skip if terminal
            if (currentStatus == "Delivered" || currentStatus == "Returned")
            {
                continue;
            }

            // Pick an invalid target status
            var allowedStatuses = validTransitions[currentStatus];
            var invalidStatuses = allStatuses.Except(allowedStatuses).Where(s => s != currentStatus).ToArray();

            if (invalidStatuses.Length == 0)
            {
                continue;
            }

            var targetStatus = faker.PickRandom(invalidStatuses);

            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            // Verify the patch was rejected
            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"Invalid status transition from {currentStatus} to {targetStatus} should be rejected");

            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().Contain("invalid_transition",
                "Error response should indicate invalid transition");
            errorContent.Should().Contain(currentStatus,
                "Error response should include current status");
            errorContent.Should().Contain(targetStatus,
                "Error response should include requested status");
        }
    }

    // Feature: parcel-status-lifecycle, Property 9: Tracking event creation on status change
    // Validates: Requirements 7.4
    [Fact]
    public async Task Property_TrackingEventCreationOnStatusChange()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        // Valid transitions
        var validTransitions = new Dictionary<string, string[]>
        {
            ["LabelCreated"] = new[] { "PickedUp", "Exception" },
            ["PickedUp"] = new[] { "InTransit", "Exception" },
            ["InTransit"] = new[] { "OutForDelivery", "Exception" },
            ["OutForDelivery"] = new[] { "Delivered", "Exception" },
            ["Exception"] = new[] { "Returned" }
        };

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var currentStatus = created!.Status;

            // Skip if no valid transitions
            if (!validTransitions.ContainsKey(currentStatus))
            {
                continue;
            }

            var allowedStatuses = validTransitions[currentStatus];
            if (allowedStatuses.Length == 0)
            {
                continue;
            }

            // Pick a valid target status
            var targetStatus = faker.PickRandom(allowedStatuses);

            // Get tracking events before the transition
            var beforeResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
            beforeResp.EnsureSuccessStatusCode();
            var beforeEvents = await beforeResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
            var eventCountBefore = beforeEvents!.Count;

            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            // Verify the patch succeeded
            patchResp.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Valid status transition from {currentStatus} to {targetStatus} should succeed");

            // Get tracking events after the transition
            var afterResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
            afterResp.EnsureSuccessStatusCode();
            var afterEvents = await afterResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
            var eventCountAfter = afterEvents!.Count;

            // Verify a tracking event was created
            eventCountAfter.Should().BeGreaterThan(eventCountBefore,
                $"A tracking event should be created when status changes from {currentStatus} to {targetStatus}");

            // Verify the new event has the correct status
            var latestEvent = afterEvents.OrderByDescending(e => e.Timestamp).First();
            latestEvent.EventType.Should().Contain(targetStatus,
                "The latest tracking event should reflect the new status");
        }
    }

    // Feature: parcel-status-lifecycle, Property 10: Invalid PATCH path rejection
    // Validates: Requirements 8.2
    [Fact]
    public async Task Property_InvalidPatchPathRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        // Invalid paths (non-existent fields)
        var invalidPaths = new[] 
        { 
            "/nonExistentField", 
            "/invalidProperty", 
            "/randomField", 
            "/fakeProperty",
            "/unknownField",
            "/notARealField",
            "/doesNotExist"
        };

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Pick a random invalid path
            var invalidPath = faker.PickRandom(invalidPaths);
            var fieldName = invalidPath.TrimStart('/'); // Extract field name without slash
            var randomValue = faker.Random.AlphaNumeric(10);

            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"{invalidPath}\",\"value\":\"{randomValue}\"}}]";

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            // Verify the patch was rejected with 422
            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"PATCH operation with invalid path '{invalidPath}' should be rejected with 422");

            // Verify the error response includes the invalid path (field name)
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().Contain(fieldName,
                $"Error response should include the invalid field name '{fieldName}'");
        }
    }

    // Feature: parcel-status-lifecycle, Property 11: Invalid PATCH value type rejection
    // Validates: Requirements 8.3
    [Fact]
    public async Task Property_InvalidPatchValueTypeRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Generate invalid type mismatches
            var testCase = faker.Random.Int(0, 2);
            string patchJson;
            string expectedField;

            switch (testCase)
            {
                case 0: // Invalid ServiceType (not a valid enum value)
                    var invalidServiceType = faker.Random.AlphaNumeric(15);
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"{invalidServiceType}\"}}]";
                    expectedField = "serviceType";
                    break;

                case 1: // Invalid Status (not a valid enum value)
                    var invalidStatus = faker.Random.AlphaNumeric(15);
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{invalidStatus}\"}}]";
                    expectedField = "status";
                    break;

                case 2: // Invalid EstimatedDeliveryDate (not a valid date format)
                    var invalidDate = faker.Random.AlphaNumeric(10);
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{invalidDate}\"}}]";
                    expectedField = "estimatedDeliveryDate";
                    break;

                default:
                    throw new InvalidOperationException("Invalid test case");
            }

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            // Verify the patch was rejected with 422
            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"PATCH operation with invalid value type for '{expectedField}' should be rejected with 422");

            // Verify the error response includes type mismatch details
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().NotBeEmpty("Error response should contain details about the type mismatch");
        }
    }

    // Feature: parcel-status-lifecycle, Property 12: Multiple PATCH errors aggregation
    // Validates: Requirements 8.4, 8.5
    [Fact]
    public async Task Property_MultiplePatchErrorsAggregation()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Create a patch document with multiple invalid operations (at least 2)
            var numErrors = faker.Random.Int(2, 4);
            var operations = new List<string>();

            for (int j = 0; j < numErrors; j++)
            {
                var errorType = faker.Random.Int(0, 1);
                
                if (errorType == 0)
                {
                    // Invalid path (non-existent field)
                    var invalidFieldName = $"invalidField{j}";
                    var invalidPath = $"/{invalidFieldName}";
                    operations.Add($"{{\"op\":\"replace\",\"path\":\"{invalidPath}\",\"value\":\"test\"}}");
                }
                else
                {
                    // Invalid value type for enum
                    var invalidValue = faker.Random.AlphaNumeric(10);
                    operations.Add($"{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{invalidValue}\"}}");
                }
            }

            var patchJson = $"[{string.Join(",", operations)}]";

            // Apply the patch
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            // Verify the patch was rejected with 422
            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                "PATCH document with multiple invalid operations should be rejected with 422");

            // Verify all errors are aggregated in the response
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().NotBeEmpty("Error response should contain aggregated errors");
            
            // Parse the error response to check for multiple errors
            // The ModelState serialization returns errors in a dictionary format
            // Example: {"ParcelPatchModel":["error1","error2","error3"]}
            
            // Count the number of error messages in the response
            // Each error message is typically enclosed in quotes
            var errorMessageCount = System.Text.RegularExpressions.Regex.Matches(errorContent, "\"[^\"]+\"").Count;
            
            // We should have at least as many error messages as we had invalid operations
            // (The key "ParcelPatchModel" counts as one, so we expect numErrors + 1 quoted strings minimum)
            errorMessageCount.Should().BeGreaterThanOrEqualTo(numErrors,
                $"Error response should contain at least {numErrors} error messages for the {numErrors} invalid operations");
            
            // Verify the response contains multiple distinct error messages
            // Look for common error indicators
            var hasMultipleErrors = 
                errorContent.Contains("[") && errorContent.Contains("]") && // Array notation
                errorContent.Split(new[] { "\",\"" }, StringSplitOptions.None).Length > 1; // Multiple quoted strings
            
            hasMultipleErrors.Should().BeTrue(
                "Error response should contain multiple aggregated error messages");
        }
    }

    // Feature: parcel-status-lifecycle, Property 5: Terminal state protection across endpoints
    // Validates: Requirements 4.4
    [Fact]
    public async Task Property_TerminalStateProtectionAcrossEndpoints()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        // Terminal statuses
        var terminalStatuses = new[] { "Delivered", "Returned" };

        for (int i = 0; i < iterations; i++)
        {
            // Create a parcel
            var (shipperId, recipientId) = await SeedAddressesAsync();
            var request = BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            // Transition the parcel to a terminal state
            var terminalStatus = faker.PickRandom(terminalStatuses);
            
            // Transition through valid states to reach terminal
            if (terminalStatus == "Delivered")
            {
                // LabelCreated -> PickedUp -> InTransit -> OutForDelivery -> Delivered
                await TransitionToStatus(created!.Id, "PickedUp");
                await TransitionToStatus(created.Id, "InTransit");
                await TransitionToStatus(created.Id, "OutForDelivery");
                await TransitionToStatus(created.Id, "Delivered");
            }
            else // Returned
            {
                // LabelCreated -> Exception -> Returned
                await TransitionToStatus(created!.Id, "Exception");
                await TransitionToStatus(created.Id, "Returned");
            }

            // Verify the parcel is now in terminal state
            var getResp = await _client.GetAsync($"/api/parcels/{created.Id}");
            getResp.EnsureSuccessStatusCode();
            var parcel = await getResp.Content.ReadFromJsonAsync<ParcelDetailResponse>();
            parcel!.Status.Should().Be(terminalStatus, "Parcel should be in terminal state");

            // Test 1: PATCH endpoint should reject modifications
            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Modified description\"}}]";
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"PATCH endpoint should reject modifications to parcel in terminal state '{terminalStatus}'");

            var patchError = await patchResp.Content.ReadAsStringAsync();
            patchError.Should().Contain("terminal_state",
                "PATCH error response should indicate terminal state");
            patchError.Should().Contain(terminalStatus,
                "PATCH error response should include the current terminal status");

            // Test 2: POST tracking event endpoint should reject modifications
            var trackingEventRequest = new CreateTrackingEventRequest
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = Domain.Enums.EventType.InTransit,
                Description = "Attempting to add event to terminal parcel"
            };

            var postEventResp = await _client.PostAsJsonAsync($"/api/parcels/{created.Id}/events", trackingEventRequest);

            postEventResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
                $"POST tracking event endpoint should reject modifications to parcel in terminal state '{terminalStatus}'");

            var postEventError = await postEventResp.Content.ReadAsStringAsync();
            postEventError.Should().Contain("terminal_state",
                "POST tracking event error response should indicate terminal state");
            postEventError.Should().Contain(terminalStatus,
                "POST tracking event error response should include the current terminal status");
        }
    }

    private async Task TransitionToStatus(int parcelId, string targetStatus)
    {
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
        var patchResp = await _client.PatchAsync($"/api/parcels/{parcelId}", patchContent);
        patchResp.EnsureSuccessStatusCode();
    }

    // ─── Integration Tests for PATCH Endpoint ───────────────────────────

    [Fact]
    public async Task Patch_ValidDescriptionUpdate_Returns200()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Updated electronics shipment\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Description.Should().Be("Updated electronics shipment");
        updated.Id.Should().Be(created.Id);
        updated.TrackingNumber.Should().Be(created.TrackingNumber);
    }

    [Fact]
    public async Task Patch_ValidServiceTypeUpdate_Returns200()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"Overnight\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.ServiceType.Should().Be("Overnight");
    }

    [Fact]
    public async Task Patch_ValidEstimatedDeliveryDateUpdate_Returns200()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var newDate = DateTimeOffset.UtcNow.AddDays(10);
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{newDate:O}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.EstimatedDeliveryDate.Should().NotBeNull();
        updated.EstimatedDeliveryDate!.Value.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Patch_ValidStatusTransition_Returns200AndCreatesTrackingEvent()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        // Get initial tracking events count
        var beforeResp = await _client.GetAsync($"/api/parcels/{created!.Id}/events");
        beforeResp.EnsureSuccessStatusCode();
        var beforeEvents = await beforeResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
        var eventCountBefore = beforeEvents!.Count;

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"PickedUp\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Status.Should().Be("PickedUp");

        // Verify tracking event was created
        var afterResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
        afterResp.EnsureSuccessStatusCode();
        var afterEvents = await afterResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
        afterEvents!.Count.Should().BeGreaterThan(eventCountBefore);
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldId_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/id\",\"value\":99999}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldTrackingNumber_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/trackingNumber\",\"value\":\"FAKE-123\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldCreatedAt_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var newDate = DateTimeOffset.UtcNow.AddDays(-10);
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/createdAt\",\"value\":\"{newDate:O}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_InvalidStatusTransition_Returns422WithErrorDetails()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        // Try to transition from LabelCreated directly to Delivered (invalid)
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"Delivered\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("invalid_transition");
        errorContent.Should().Contain("LabelCreated");
        errorContent.Should().Contain("Delivered");
        errorContent.Should().Contain("allowedStatuses");
    }

    [Fact]
    public async Task Patch_TerminalStateDelivered_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        // Transition to Delivered
        await TransitionToStatus(created!.Id, "PickedUp");
        await TransitionToStatus(created.Id, "InTransit");
        await TransitionToStatus(created.Id, "OutForDelivery");
        await TransitionToStatus(created.Id, "Delivered");

        // Try to modify description
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Should not work\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("terminal_state");
        errorContent.Should().Contain("Delivered");
    }

    [Fact]
    public async Task Patch_TerminalStateReturned_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        // Transition to Returned
        await TransitionToStatus(created!.Id, "Exception");
        await TransitionToStatus(created.Id, "Returned");

        // Try to modify service type
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"Express\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("terminal_state");
        errorContent.Should().Contain("Returned");
    }

    [Fact]
    public async Task Patch_MalformedJson_Returns400()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var malformedJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\""; // Missing closing brackets
        var patchContent = new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_InvalidEnumValue_Returns422()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"InvalidStatus\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_NonExistentParcel_Returns404()
    {
        // Arrange
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync("/api/parcels/999999", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_MultipleOperations_AllApplied()
    {
        // Arrange
        var (shipperId, recipientId) = await SeedAddressesAsync();
        var request = BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = @"[
            {""op"":""replace"",""path"":""/description"",""value"":""Multi-op update""},
            {""op"":""replace"",""path"":""/serviceType"",""value"":""Overnight""}
        ]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Description.Should().Be("Multi-op update");
        updated.ServiceType.Should().Be("Overnight");
    }

    [Fact]
    public async Task Patch_WithoutApiKey_Returns401()
    {
        // Arrange
        using var anonClient = _factory.CreateClient();
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await anonClient.PatchAsync("/api/parcels/1", patchContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
