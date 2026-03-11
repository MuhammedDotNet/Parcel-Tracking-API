using Bogus;
using FluentAssertions;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Validators;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.UnitTests.Services;

public class TrackingServiceValidationTests
{
    // Feature: tracking-events-history, Property 11: Description length validation
    // Validates: Requirements 7.4
    [Fact]
    public void Property_DescriptionLengthValidation_ValidatesMaxLength()
    {
        const int iterations = 100;
        var faker = new Faker();
        var validator = new CreateTrackingEventRequestValidator();

        for (int i = 0; i < iterations; i++)
        {
            var longDescription = faker.Random.String2(501, 1000);

            var request = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = longDescription,
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country()
            };

            var result = validator.Validate(request);

            result.IsValid.Should().BeFalse(
                $"description with {longDescription.Length} characters should fail validation");
            
            result.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.Description) &&
                e.ErrorMessage.Contains("500"),
                "validation error should mention the 500 character limit for Description");

            var validDescription = faker.Random.String2(1, 500);
            var validRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = validDescription,
                LocationCity = faker.Address.City(),
                LocationState = faker.Address.State(),
                LocationCountry = faker.Address.Country()
            };

            var validResult = validator.Validate(validRequest);

            validResult.Errors.Should().NotContain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.Description) &&
                e.ErrorMessage.Contains("500"),
                $"description with {validDescription.Length} characters should not fail length validation");
        }
    }

    // Feature: tracking-events-history, Property 12: Location field length validation
    // Validates: Requirements 7.5
    [Fact]
    public void Property_LocationFieldLengthValidation_ValidatesMaxLength()
    {
        const int iterations = 100;
        var faker = new Faker();
        var validator = new CreateTrackingEventRequestValidator();

        for (int i = 0; i < iterations; i++)
        {
            var longCity = faker.Random.String2(101, 200);
            var cityRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCity = longCity
            };

            var cityResult = validator.Validate(cityRequest);
            cityResult.IsValid.Should().BeFalse(
                $"LocationCity with {longCity.Length} characters should fail validation");
            cityResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationCity) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationCity");

            var longState = faker.Random.String2(101, 200);
            var stateRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationState = longState
            };

            var stateResult = validator.Validate(stateRequest);
            stateResult.IsValid.Should().BeFalse(
                $"LocationState with {longState.Length} characters should fail validation");
            stateResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationState) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationState");

            var longCountry = faker.Random.String2(101, 200);
            var countryRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCountry = longCountry
            };

            var countryResult = validator.Validate(countryRequest);
            countryResult.IsValid.Should().BeFalse(
                $"LocationCountry with {longCountry.Length} characters should fail validation");
            countryResult.Errors.Should().Contain(e => 
                e.PropertyName == nameof(CreateTrackingEventRequest.LocationCountry) &&
                e.ErrorMessage.Contains("100"),
                "validation error should mention the 100 character limit for LocationCountry");

            var validCity = faker.Random.String2(1, 100);
            var validState = faker.Random.String2(1, 100);
            var validCountry = faker.Random.String2(1, 100);
            var validRequest = new CreateTrackingEventRequest
            {
                Timestamp = faker.Date.RecentOffset(days: 1),
                EventType = faker.PickRandom<EventType>(),
                Description = faker.Lorem.Sentence(),
                LocationCity = validCity,
                LocationState = validState,
                LocationCountry = validCountry
            };

            var validResult = validator.Validate(validRequest);

            validResult.Errors.Should().NotContain(e => 
                (e.PropertyName == nameof(CreateTrackingEventRequest.LocationCity) ||
                 e.PropertyName == nameof(CreateTrackingEventRequest.LocationState) ||
                 e.PropertyName == nameof(CreateTrackingEventRequest.LocationCountry)) &&
                e.ErrorMessage.Contains("100"),
                "valid location fields should not fail length validation");
        }
    }
}
