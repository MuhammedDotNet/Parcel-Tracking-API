using FluentValidation;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Validators;

/// <summary>
/// Validates <see cref="ConfirmDeliveryRequest"/> input data.
/// </summary>
public class ConfirmDeliveryRequestValidator : AbstractValidator<ConfirmDeliveryRequest>
{
    public ConfirmDeliveryRequestValidator()
    {
        RuleFor(x => x.ReceivedBy)
            .NotEmpty()
            .WithMessage("ReceivedBy is required.")
            .MaximumLength(200)
            .WithMessage("ReceivedBy must not exceed 200 characters.");

        RuleFor(x => x.DeliveryLocation)
            .NotEmpty()
            .WithMessage("DeliveryLocation is required.")
            .MaximumLength(200)
            .WithMessage("DeliveryLocation must not exceed 200 characters.");

        RuleFor(x => x.DeliveredAt)
            .NotEmpty()
            .WithMessage("DeliveredAt is required.")
            .Must(date => date <= DateTimeOffset.UtcNow.AddMinutes(1))
            .WithMessage("DeliveredAt cannot be in the future.");

        RuleFor(x => x.SignatureImage)
            .Must(BeValidBase64)
            .When(x => !string.IsNullOrEmpty(x.SignatureImage))
            .WithMessage("SignatureImage must be a valid base64-encoded string.");
    }

    private static bool BeValidBase64(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
