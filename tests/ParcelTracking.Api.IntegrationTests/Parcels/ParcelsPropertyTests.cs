using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

[Collection("Database")]
public class ParcelsPropertyTests : IAsyncLifetime
{
    private readonly HttpClient _client;

    public ParcelsPropertyTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    // Feature: parcel-status-lifecycle, Property 6: Valid PATCH operations are applied
    [Fact]
    public async Task Property_ValidPatchOperationsAreApplied()
    {
        const int iterations = 10;
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var fieldChoice = faker.Random.Int(0, 2);
            string patchJson;
            object expectedValue;

            switch (fieldChoice)
            {
                case 0:
                    var serviceTypes = new[] { "Standard", "Express", "Overnight", "Economy" };
                    var newServiceType = faker.PickRandom(serviceTypes);
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"{newServiceType}\"}}]";
                    expectedValue = newServiceType;
                    break;
                case 1:
                    var newDescription = faker.Lorem.Sentence().Replace("\"", "'");
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"{newDescription}\"}}]";
                    expectedValue = newDescription;
                    break;
                case 2:
                    var newDate = DateTimeOffset.UtcNow.AddDays(faker.Random.Int(1, 30));
                    patchJson = $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{newDate:O}\"}}]";
                    expectedValue = newDate;
                    break;
                default:
                    throw new InvalidOperationException("Invalid field choice");
            }

            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await patchResp.Content.ReadFromJsonAsync<ParcelResponse>();

            switch (fieldChoice)
            {
                case 0:
                    updated!.ServiceType.Should().Be((string)expectedValue);
                    break;
                case 1:
                    updated!.Description.Should().Be((string)expectedValue);
                    break;
                case 2:
                    updated!.EstimatedDeliveryDate!.Value.Should().BeCloseTo((DateTimeOffset)expectedValue, TimeSpan.FromSeconds(1));
                    break;
            }
        }
    }

    // Feature: parcel-status-lifecycle, Property 7: Non-whitelisted field PATCH rejection
    [Fact]
    public async Task Property_NonWhitelistedFieldPatchRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();
        var nonWhitelistedFields = new[] { "id", "trackingNumber", "createdAt", "shipperAddressId", "recipientAddressId" };

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var field = faker.PickRandom(nonWhitelistedFields);
            var newValue = faker.Random.Int(1, 10000);
            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/{field}\",\"value\":{newValue}}}]";

            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
    }

    // Feature: parcel-status-lifecycle, Property 8: Status transition validation via PATCH
    [Fact]
    public async Task Property_StatusTransitionValidationViaPatch()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

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
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var currentStatus = created!.Status;
            if (currentStatus == "Delivered" || currentStatus == "Returned") continue;

            var allowedStatuses = validTransitions[currentStatus];
            var invalidStatuses = allStatuses.Except(allowedStatuses).Where(s => s != currentStatus).ToArray();
            if (invalidStatuses.Length == 0) continue;

            var targetStatus = faker.PickRandom(invalidStatuses);
            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";

            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().Contain("invalid_transition");
        }
    }

    // Feature: parcel-status-lifecycle, Property 9: Tracking event creation on status change
    [Fact]
    public async Task Property_TrackingEventCreationOnStatusChange()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

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
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var currentStatus = created!.Status;
            if (!validTransitions.ContainsKey(currentStatus)) continue;

            var allowedStatuses = validTransitions[currentStatus];
            if (allowedStatuses.Length == 0) continue;

            var targetStatus = faker.PickRandom(allowedStatuses);

            var beforeResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
            beforeResp.EnsureSuccessStatusCode();
            var beforeEvents = await beforeResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
            var eventCountBefore = beforeEvents!.Count;

            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var afterResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
            afterResp.EnsureSuccessStatusCode();
            var afterEvents = await afterResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();

            afterEvents!.Count.Should().BeGreaterThan(eventCountBefore);
        }
    }

    // Feature: parcel-status-lifecycle, Property 10: Invalid PATCH path rejection
    [Fact]
    public async Task Property_InvalidPatchPathRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();
        var invalidPaths = new[] { "/nonExistentField", "/invalidProperty", "/randomField", "/fakeProperty" };

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var invalidPath = faker.PickRandom(invalidPaths);
            var fieldName = invalidPath.TrimStart('/');
            var randomValue = faker.Random.AlphaNumeric(10);
            var patchJson = $"[{{\"op\":\"replace\",\"path\":\"{invalidPath}\",\"value\":\"{randomValue}\"}}]";

            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().Contain(fieldName);
        }
    }

    // Feature: parcel-status-lifecycle, Property 11: Invalid PATCH value type rejection
    [Fact]
    public async Task Property_InvalidPatchValueTypeRejection()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var testCase = faker.Random.Int(0, 2);
            string patchJson = testCase switch
            {
                0 => $"[{{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"{faker.Random.AlphaNumeric(15)}\"}}]",
                1 => $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{faker.Random.AlphaNumeric(15)}\"}}]",
                _ => $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{faker.Random.AlphaNumeric(10)}\"}}]"
            };

            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
    }

    // Feature: parcel-status-lifecycle, Property 12: Multiple PATCH errors aggregation
    [Fact]
    public async Task Property_MultiplePatchErrorsAggregation()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var numErrors = faker.Random.Int(2, 4);
            var operations = new List<string>();

            for (int j = 0; j < numErrors; j++)
            {
                var errorType = faker.Random.Int(0, 1);
                if (errorType == 0)
                {
                    var invalidFieldName = $"invalidField{j}";
                    operations.Add($"{{\"op\":\"replace\",\"path\":\"/{invalidFieldName}\",\"value\":\"test\"}}");
                }
                else
                {
                    var invalidValue = faker.Random.AlphaNumeric(10);
                    operations.Add($"{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{invalidValue}\"}}");
                }
            }

            var patchJson = $"[{string.Join(",", operations)}]";
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var errorContent = await patchResp.Content.ReadAsStringAsync();
            errorContent.Should().NotBeEmpty();
        }
    }

    // Feature: parcel-status-lifecycle, Property 5: Terminal state protection across endpoints
    [Fact]
    public async Task Property_TerminalStateProtectionAcrossEndpoints()
    {
        const int iterations = 100;
        var faker = new Bogus.Faker();
        var terminalStatuses = new[] { "Delivered", "Returned" };

        for (int i = 0; i < iterations; i++)
        {
            var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
            var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
            var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
            createResp.EnsureSuccessStatusCode();
            var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

            var terminalStatus = faker.PickRandom(terminalStatuses);

            if (terminalStatus == "Delivered")
            {
                await ParcelTestHelpers.TransitionToStatus(_client, created!.Id, "PickedUp");
                await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "InTransit");
                await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "OutForDelivery");
                await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "Delivered");
            }
            else
            {
                await ParcelTestHelpers.TransitionToStatus(_client, created!.Id, "Exception");
                await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "Returned");
            }

            var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Modified\"}]";
            var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
            var patchResp = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

            patchResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var patchError = await patchResp.Content.ReadAsStringAsync();
            patchError.Should().Contain("terminal_state");

            var trackingEventRequest = new CreateTrackingEventRequest
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = Domain.Enums.EventType.InTransit,
                Description = "Attempting to add event"
            };

            var postEventResp = await _client.PostAsJsonAsync($"/api/parcels/{created.Id}/events", trackingEventRequest);
            postEventResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
    }

    private readonly IntegrationTestFixture _fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
