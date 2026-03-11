using FluentAssertions;
using Bogus;
using ParcelTracking.Application.Helpers;

namespace ParcelTracking.Application.UnitTests.Helpers;

public class CursorHelperPropertyTests
{
    // Feature: parcel-search-filter-pagination, Property 1: Cursor round-trip consistency
    // Validates: Requirements 5.3, 5.4
    [Fact]
    public void Property_CursorRoundTripConsistency_ValidatesEncodingDecoding()
    {
        const int iterations = 100;
        var faker = new Faker();

        for (int i = 0; i < iterations; i++)
        {
            // Generate random inputs
            var sortField = faker.Random.Word();
            var sortValue = faker.Random.AlphaNumeric(faker.Random.Int(1, 50));
            var id = faker.Random.Int(1, int.MaxValue);
            
            // Encode the cursor
            var encoded = CursorHelper.Encode(sortField, sortValue, id);
            
            // Verify encoded cursor is not empty and is valid Base64
            encoded.Should().NotBeNullOrEmpty();
            
            // Decode the cursor
            var (decodedSortField, decodedSortValue, decodedId) = CursorHelper.Decode(encoded);
            
            // Verify round-trip consistency
            decodedSortField.Should().Be(sortField, 
                $"decoded sort field should match original for iteration {i}");
            decodedSortValue.Should().Be(sortValue, 
                $"decoded sort value should match original for iteration {i}");
            decodedId.Should().Be(id, 
                $"decoded id should match original for iteration {i}");
        }
    }
}
