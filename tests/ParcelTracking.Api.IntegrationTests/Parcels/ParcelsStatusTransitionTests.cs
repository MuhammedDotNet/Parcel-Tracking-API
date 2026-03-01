using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsStatusTransitionTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;

    public ParcelsStatusTransitionTests(ParcelTrackingWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Patch_ValidStatusTransition_Returns200AndCreatesTrackingEvent()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var beforeResp = await _client.GetAsync($"/api/parcels/{created!.Id}/events");
        beforeResp.EnsureSuccessStatusCode();
        var beforeEvents = await beforeResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
        var eventCountBefore = beforeEvents!.Count;

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"PickedUp\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ParcelResponse>();
        updated!.Status.Should().Be("PickedUp");

        var afterResp = await _client.GetAsync($"/api/parcels/{created.Id}/events");
        afterResp.EnsureSuccessStatusCode();
        var afterEvents = await afterResp.Content.ReadFromJsonAsync<List<TrackingEventResponse>>();
        afterEvents!.Count.Should().BeGreaterThan(eventCountBefore);
    }

    [Fact]
    public async Task Patch_InvalidStatusTransition_Returns422WithErrorDetails()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"Delivered\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created!.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("invalid_transition");
        errorContent.Should().Contain("LabelCreated");
        errorContent.Should().Contain("Delivered");
        errorContent.Should().Contain("allowedStatuses");
    }
}
