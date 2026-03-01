using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsPatchTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public ParcelsPatchTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Patch_ValidDescriptionUpdate_Returns200()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Updated electronics shipment\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Description.Should().Be("Updated electronics shipment");
        updated.Id.Should().Be(created.Id);
        updated.TrackingNumber.Should().Be(created.TrackingNumber);
    }

    [Fact]
    public async Task Patch_ValidServiceTypeUpdate_Returns200()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"Overnight\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.ServiceType.Should().Be("Overnight");
    }

    [Fact]
    public async Task Patch_ValidEstimatedDeliveryDateUpdate_Returns200()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var newDate = DateTimeOffset.UtcNow.AddDays(10);
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/estimatedDeliveryDate\",\"value\":\"{newDate:O}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.EstimatedDeliveryDate.Should().NotBeNull();
        updated.EstimatedDeliveryDate!.Value.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Patch_MultipleOperations_AllApplied()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = @"[
            {""op"":""replace"",""path"":""/description"",""value"":""Multi-op update""},
            {""op"":""replace"",""path"":""/serviceType"",""value"":""Overnight""}
        ]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Description.Should().Be("Multi-op update");
        updated.ServiceType.Should().Be("Overnight");
    }

    [Fact]
    public async Task Patch_NonExistentParcel_Returns404()
    {
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync("/api/parcels/999999", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_MalformedJson_Returns400()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var malformedJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\"";
        var patchContent = new StringContent(malformedJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_WithoutApiKey_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"test\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await anonClient.PatchAsync("/api/parcels/1", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
