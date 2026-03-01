using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public class ParcelsRetrievalTests : IClassFixture<ParcelTrackingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly ParcelTrackingWebAppFactory _factory;

    public ParcelsRetrievalTests(ParcelTrackingWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    [Fact]
    public async Task GetById_WhenExists_Returns200WithDetails()
    {
        var (shipperId, recipientId) = await ParcelTestHelpers.SeedAddressesAsync(_client);
        var request = ParcelTestHelpers.BuildRequest(shipperId, recipientId);

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
}
