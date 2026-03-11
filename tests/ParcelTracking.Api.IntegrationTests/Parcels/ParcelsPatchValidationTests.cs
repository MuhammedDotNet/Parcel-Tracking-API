using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

[Collection("Database")]
public class ParcelsPatchValidationTests : IAsyncLifetime
{
    private readonly HttpClient _client;

    public ParcelsPatchValidationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldId_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/id\",\"value\":99999}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldTrackingNumber_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/trackingNumber\",\"value\":\"FAKE-123\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_ReadOnlyFieldCreatedAt_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var newDate = DateTimeOffset.UtcNow.AddDays(-10);
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/createdAt\",\"value\":\"{newDate:O}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Patch_InvalidEnumValue_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"InvalidStatus\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private readonly IntegrationTestFixture _fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
