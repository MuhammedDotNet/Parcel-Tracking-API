using FluentAssertions;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Validators;

namespace ParcelTracking.Application.UnitTests.Validators;

public class RegisterParcelRequestValidatorTests
{
    private readonly RegisterParcelRequestValidator _sut = new();

    private static RegisterParcelRequest ValidRequest() => new()
    {
        ShipperAddressId = 1,
        RecipientAddressId = 2,
        ServiceType = "Express",
        Description = "Test parcel",
        Weight = new WeightDto { Value = 2.5m, Unit = "kg" },
        Dimensions = new DimensionsDto { Length = 40, Width = 30, Height = 10, Unit = "cm" },
        DeclaredValue = new DeclaredValueDto { Amount = 150m, Currency = "USD" },
        ContentItems =
        [
            new ContentItemDto
            {
                HsCode          = "8471.30",
                Description     = "Laptop",
                Quantity        = 1,
                UnitValue       = 150m,
                Currency        = "USD",
                Weight          = 2.1m,
                WeightUnit      = "kg",
                CountryOfOrigin = "CN"
            }
        ]
    };

    [Fact]
    public async Task Validate_ValidRequest_Passes()
    {
        var result = await _sut.ValidateAsync(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyContentItems_Fails()
    {
        var request = ValidRequest() with { ContentItems = [] };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContentItems");
    }

    [Theory]
    [InlineData("84713")]    // Missing dot
    [InlineData("847.30")]   // Only 3 digits before dot
    [InlineData("8471.3")]   // Only 1 digit after dot
    [InlineData("")]         // Empty
    [InlineData("ABCD.EF")] // Non-numeric
    public async Task Validate_InvalidHsCode_Fails(string hsCode)
    {
        var request = ValidRequest() with
        {
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode          = hsCode,
                    Description     = "Item",
                    Quantity        = 1,
                    UnitValue       = 10m,
                    Currency        = "USD",
                    Weight          = 1m,
                    WeightUnit      = "kg",
                    CountryOfOrigin = "US"
                }
            ]
        };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("HsCode"));
    }

    [Fact]
    public async Task Validate_ZeroQuantity_Fails()
    {
        var request = ValidRequest() with
        {
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode          = "8471.30",
                    Description     = "Item",
                    Quantity        = 0,
                    UnitValue       = 10m,
                    Currency        = "USD",
                    Weight          = 1m,
                    WeightUnit      = "kg",
                    CountryOfOrigin = "US"
                }
            ]
        };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Quantity"));
    }

    [Theory]
    [InlineData("USA")]  // 3 letters
    [InlineData("U")]    // 1 letter
    [InlineData("")]     // Empty
    public async Task Validate_InvalidCountryCode_Fails(string code)
    {
        var request = ValidRequest() with
        {
            ContentItems =
            [
                new ContentItemDto
                {
                    HsCode          = "8471.30",
                    Description     = "Item",
                    Quantity        = 1,
                    UnitValue       = 10m,
                    Currency        = "USD",
                    Weight          = 1m,
                    WeightUnit      = "kg",
                    CountryOfOrigin = code
                }
            ]
        };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CountryOfOrigin"));
    }

    [Theory]
    [InlineData("Turbo")]
    [InlineData("SameDay")]
    [InlineData("")]
    public async Task Validate_InvalidServiceType_Fails(string serviceType)
    {
        var request = ValidRequest() with { ServiceType = serviceType };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ServiceType");
    }

    [Fact]
    public async Task Validate_ZeroWeight_Fails()
    {
        var request = ValidRequest() with
        {
            Weight = new WeightDto { Value = 0, Unit = "kg" }
        };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight.Value");
    }

    [Theory]
    [InlineData("US")]   // 2 letters — wrong
    [InlineData("USDD")] // 4 letters
    public async Task Validate_InvalidCurrencyLength_Fails(string currency)
    {
        var request = ValidRequest() with
        {
            DeclaredValue = new DeclaredValueDto { Amount = 100m, Currency = currency }
        };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeclaredValue.Currency");
    }

    [Theory]
    [InlineData("Economy")]
    [InlineData("Standard")]
    [InlineData("Express")]
    [InlineData("Overnight")]
    [InlineData("express")]   // case-insensitive
    public async Task Validate_ValidServiceTypes_Pass(string serviceType)
    {
        var request = ValidRequest() with { ServiceType = serviceType };

        var result = await _sut.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}
