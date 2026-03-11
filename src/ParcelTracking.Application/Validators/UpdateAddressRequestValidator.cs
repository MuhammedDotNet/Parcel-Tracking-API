using FluentValidation;
using ParcelTracking.Application.DTOs;

namespace ParcelTracking.Application.Validators;

public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    private static readonly HashSet<string> ValidCountryCodes = new(StringComparer.Ordinal)
    {
        "US", "CA", "MX", "GB", "DE", "FR", "ES", "IT", "NL", "BE",
        "AU", "NZ", "JP", "KR", "CN", "IN", "BR", "AR", "CL", "CO"
    };

    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.Street1)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Street2)
            .MaximumLength(200);

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.State)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(code => ValidCountryCodes.Contains(code))
            .WithMessage("'{PropertyValue}' is not a supported country code.");

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.PostalCode)
            .Matches(@"^\d{5}(-\d{4})?$")
            .When(x => x.CountryCode == "US")
            .WithMessage("US postal code must be 5 digits or 5+4 format.");

        RuleFor(x => x.PostalCode)
            .Matches(@"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$")
            .When(x => x.CountryCode == "CA")
            .WithMessage("Canadian postal code must follow A1A 1A1 format.");

        RuleFor(x => x.ContactName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.CompanyName)
            .MaximumLength(150);

        RuleFor(x => x.Phone)
            .NotEmpty();

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
