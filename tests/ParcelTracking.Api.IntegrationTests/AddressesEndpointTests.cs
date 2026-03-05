using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests;

[Collection("Database")]
public class AddressesEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;

    public AddressesEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "dev-api-key-12345");
    }

    private static CreateAddressRequest ValidCreate() => new()
    {
        Street1 = "123 Integration St",
        City = "TestCity",
        State = "TS",
        PostalCode = "10001",
        CountryCode = "US",
        IsResidential = true,
        ContactName = "Integration User",
        Phone = "+1-555-9999",
        Email = "integration@test.com"
    };

    [Fact]
    public async Task GetAll_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithValidData_ShouldReturn201()
    {
        var response = await _client.PostAsJsonAsync("/api/addresses", ValidCreate());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<AddressResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.Street1.Should().Be("123 Integration St");
        created.ContactName.Should().Be("Integration User");
    }

    [Fact]
    public async Task Create_WithInvalidData_ShouldReturn400()
    {
        var invalid = ValidCreate() with { Street1 = "", CountryCode = "XX" };

        var response = await _client.PostAsJsonAsync("/api/addresses", invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_WhenExists_ShouldReturn200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/addresses", ValidCreate());
        var created = await createResponse.Content.ReadFromJsonAsync<AddressResponse>();

        var response = await _client.GetAsync($"/api/addresses/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var address = await response.Content.ReadFromJsonAsync<AddressResponse>();
        address!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_WhenNotExists_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/addresses/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_WhenExists_ShouldReturn200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/addresses", ValidCreate());
        var created = await createResponse.Content.ReadFromJsonAsync<AddressResponse>();

        var updateRequest = new UpdateAddressRequest
        {
            Street1 = "456 Updated Ave",
            City = "UpdatedCity",
            State = "UP",
            PostalCode = "20002",
            CountryCode = "US",
            IsResidential = false,
            ContactName = "Updated User",
            Phone = "+1-555-0001"
        };

        var response = await _client.PutAsJsonAsync($"/api/addresses/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AddressResponse>();
        updated!.Street1.Should().Be("456 Updated Ave");
        updated.City.Should().Be("UpdatedCity");
    }

    [Fact]
    public async Task Update_WhenNotExists_ShouldReturn404()
    {
        var updateRequest = new UpdateAddressRequest
        {
            Street1 = "456 Updated Ave",
            City = "UpdatedCity",
            State = "UP",
            PostalCode = "20002",
            CountryCode = "US",
            ContactName = "Updated User",
            Phone = "+1-555-0001"
        };

        var response = await _client.PutAsJsonAsync("/api/addresses/999999", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WhenExists_ShouldReturn204()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/addresses", ValidCreate());
        var created = await createResponse.Content.ReadFromJsonAsync<AddressResponse>();

        var response = await _client.DeleteAsync($"/api/addresses/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WhenNotExists_ShouldReturn404()
    {
        var response = await _client.DeleteAsync("/api/addresses/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithInvalidUSPostalCode_ShouldReturn400()
    {
        var request = ValidCreate() with { PostalCode = "ABCDE" };

        var response = await _client.PostAsJsonAsync("/api/addresses", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrudLifecycle_ShouldWork()
    {
        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/addresses", ValidCreate());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AddressResponse>();

        // Read
        var getResponse = await _client.GetAsync($"/api/addresses/{created!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Update
        var updateRequest = new UpdateAddressRequest
        {
            Street1 = "Updated Street",
            City = "UpdatedCity",
            State = "UP",
            PostalCode = "30003",
            CountryCode = "US",
            ContactName = "CycleUser",
            Phone = "+1-555-0002"
        };
        var putResponse = await _client.PutAsJsonAsync($"/api/addresses/{created.Id}", updateRequest);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<AddressResponse>();
        updated!.Street1.Should().Be("Updated Street");

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/addresses/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var verifyResponse = await _client.GetAsync($"/api/addresses/{created.Id}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private readonly IntegrationTestFixture _fixture;

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
