using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsTerminalStateTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;

    public ParcelsTerminalStateTests(ParcelTrackingWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task Patch_TerminalStateDelivered_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        await ParcelTestHelpers.TransitionToStatus(_client, created!.Id, "PickedUp");
        await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "InTransit");
        await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "OutForDelivery");
        await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "Delivered");

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/description\",\"value\":\"Should not work\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("terminal_state");
        errorContent.Should().Contain("Delivered");
    }

    [Fact]
    public async Task Patch_TerminalStateReturned_Returns422()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);
        var createResp = await _client.PostAsJsonAsync("/api/parcels", request);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ParcelResponse>();

        await ParcelTestHelpers.TransitionToStatus(_client, created!.Id, "Exception");
        await ParcelTestHelpers.TransitionToStatus(_client, created.Id, "Returned");

        var patchJson = "[{\"op\":\"replace\",\"path\":\"/serviceType\",\"value\":\"Express\"}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync($"/api/parcels/{created.Id}", patchContent);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("terminal_state");
        errorContent.Should().Contain("Returned");
    }
}
