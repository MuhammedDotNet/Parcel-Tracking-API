using FluentAssertions;
using FluentValidation.TestHelper;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Validators;

namespace ParcelTracking.Application.UnitTests.Validators;

public class CreateAddressRequestValidatorTests
{
    private readonly CreateAddressRequestValidator _validator = new();

    private static CreateAddressRequest ValidRequest() => new()
    {
        Street1 = "123 Main St",
        City = "New York",
        State = "NY",
        PostalCode = "10001",
        CountryCode = "US",
        IsResidential = true,
        ContactName = "John Doe",
        Phone = "+1-555-0100"
    };

    [Fact]
    public void ValidRequest_ShouldPassValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Street1_WhenEmpty_ShouldFail(string? street1)
    {
        var request = ValidRequest() with { Street1 = street1! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Street1);
    }

    [Fact]
    public void Street1_WhenTooLong_ShouldFail()
    {
        var request = ValidRequest() with { Street1 = new string('A', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Street1);
    }

    [Fact]
    public void Street2_WhenTooLong_ShouldFail()
    {
        var request = ValidRequest() with { Street2 = new string('A', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Street2);
    }

    [Fact]
    public void Street2_WhenNull_ShouldPass()
    {
        var request = ValidRequest() with { Street2 = null };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Street2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void City_WhenEmpty_ShouldFail(string? city)
    {
        var request = ValidRequest() with { City = city! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void City_WhenTooLong_ShouldFail()
    {
        var request = ValidRequest() with { City = new string('A', 101) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void State_WhenEmpty_ShouldFail(string? state)
    {
        var request = ValidRequest() with { State = state! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.State);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("RU")]
    public void CountryCode_WhenUnsupported_ShouldFail(string code)
    {
        var request = ValidRequest() with { CountryCode = code, PostalCode = "00000" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CountryCode);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("CA")]
    [InlineData("GB")]
    [InlineData("DE")]
    public void CountryCode_WhenSupported_ShouldPass(string code)
    {
        var postalCode = code switch
        {
            "US" => "10001",
            "CA" => "K1A 0B1",
            _ => "12345"
        };
        var request = ValidRequest() with { CountryCode = code, PostalCode = postalCode };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CountryCode);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("USA")]
    public void CountryCode_WhenWrongLength_ShouldFail(string code)
    {
        var request = ValidRequest() with { CountryCode = code };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CountryCode);
    }

    [Theory]
    [InlineData("10001")]
    [InlineData("90210")]
    [InlineData("10001-1234")]
    public void PostalCode_US_ValidFormats_ShouldPass(string postalCode)
    {
        var request = ValidRequest() with { CountryCode = "US", PostalCode = postalCode };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PostalCode);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("ABCDE")]
    [InlineData("100011")]
    public void PostalCode_US_InvalidFormats_ShouldFail(string postalCode)
    {
        var request = ValidRequest() with { CountryCode = "US", PostalCode = postalCode };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PostalCode);
    }

    [Theory]
    [InlineData("K1A 0B1")]
    [InlineData("K1A0B1")]
    public void PostalCode_CA_ValidFormats_ShouldPass(string postalCode)
    {
        var request = ValidRequest() with { CountryCode = "CA", PostalCode = postalCode };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.PostalCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("k1a 0b1")]
    public void PostalCode_CA_InvalidFormats_ShouldFail(string postalCode)
    {
        var request = ValidRequest() with { CountryCode = "CA", PostalCode = postalCode };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PostalCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContactName_WhenEmpty_ShouldFail(string? contactName)
    {
        var request = ValidRequest() with { ContactName = contactName! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ContactName);
    }

    [Fact]
    public void ContactName_WhenTooLong_ShouldFail()
    {
        var request = ValidRequest() with { ContactName = new string('A', 151) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ContactName);
    }

    [Fact]
    public void CompanyName_WhenTooLong_ShouldFail()
    {
        var request = ValidRequest() with { CompanyName = new string('A', 151) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void CompanyName_WhenNull_ShouldPass()
    {
        var request = ValidRequest() with { CompanyName = null };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CompanyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Phone_WhenEmpty_ShouldFail(string? phone)
    {
        var request = ValidRequest() with { Phone = phone! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-an-email")]
    public void Email_WhenInvalid_ShouldFail(string email)
    {
        var request = ValidRequest() with { Email = email };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenValid_ShouldPass()
    {
        var request = ValidRequest() with { Email = "test@example.com" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenNull_ShouldPass()
    {
        var request = ValidRequest() with { Email = null };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenEmpty_ShouldPass()
    {
        var request = ValidRequest() with { Email = "" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
