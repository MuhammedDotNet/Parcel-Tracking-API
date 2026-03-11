using System.Net.Http.Json;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Api.IntegrationTests.Parcels;

public static class ParcelTestHelpers
{
    public static async Task<(int ShipperId, int RecipientId)> SeedAddressesAsync(HttpClient client)
    {
        var shipperResp = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
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

        var recipientResp = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
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

    public static RegisterParcelRequest BuildRequest(int shipperId, int recipientId) => new()
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
                HsCode = "8471.30",
                Description = "Portable laptop computer",
                Quantity = 1,
                UnitValue = 1200m,
                Currency = "USD",
                Weight = 2.1m,
                WeightUnit = "kg",
                CountryOfOrigin = "CN"
            }
        ]
    };

    public static async Task TransitionToStatus(HttpClient client, int parcelId, string targetStatus)
    {
        var patchJson = $"[{{\"op\":\"replace\",\"path\":\"/status\",\"value\":\"{targetStatus}\"}}]";
        var patchContent = new StringContent(patchJson, System.Text.Encoding.UTF8, "application/json-patch+json");
        var patchResp = await client.PatchAsync($"/api/parcels/{parcelId}", patchContent);
        patchResp.EnsureSuccessStatusCode();
    }
}
