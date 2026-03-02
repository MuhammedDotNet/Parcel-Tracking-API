using FluentAssertions;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Application.Validators;

namespace ParcelTracking.Application.UnitTests.Validators;

public class ConfirmDeliveryRequestValidatorTests
{
    private readonly ConfirmDeliveryRequestValidator _validator = new();

    private static ConfirmDeliveryRequest CreateValidRequest() => new()
    {
        ReceivedBy = "John Doe",
        DeliveryLocation = "Front door",
        DeliveredAt = DateTimeOffset.UtcNow.AddHours(-1)
    };

    [Fact]
    public void ValidRequest_ShouldPass()
    {
        var result = _validator.Validate(CreateValidRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingReceivedBy_ShouldFail()
    {
        var request = CreateValidRequest() with { ReceivedBy = "" };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.ReceivedBy) &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void MissingDeliveryLocation_ShouldFail()
    {
        var request = CreateValidRequest() with { DeliveryLocation = "" };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.DeliveryLocation) &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void FutureDeliveredAt_ShouldFail()
    {
        var request = CreateValidRequest() with { DeliveredAt = DateTimeOffset.UtcNow.AddDays(1) };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.DeliveredAt) &&
            e.ErrorMessage.Contains("future"));
    }

    [Fact]
    public void ValidBase64Signature_ShouldPass()
    {
        var request = CreateValidRequest() with
        {
            SignatureImage = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 })
        };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void InvalidBase64Signature_ShouldFail()
    {
        var request = CreateValidRequest() with { SignatureImage = "not-valid-base64!@#" };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.SignatureImage) &&
            e.ErrorMessage.Contains("base64"));
    }

    [Fact]
    public void NullSignature_ShouldPass()
    {
        var request = CreateValidRequest() with { SignatureImage = null };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ReceivedByExceedsMaxLength_ShouldFail()
    {
        var request = CreateValidRequest() with { ReceivedBy = new string('A', 201) };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.ReceivedBy));
    }

    [Fact]
    public void DeliveryLocationExceedsMaxLength_ShouldFail()
    {
        var request = CreateValidRequest() with { DeliveryLocation = new string('B', 201) };
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ConfirmDeliveryRequest.DeliveryLocation));
    }
}
