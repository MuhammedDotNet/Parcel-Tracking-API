using FluentValidation;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;
using System.Text.RegularExpressions;

namespace ParcelTracking.Application.Validators;

public sealed class RegisterParcelRequestValidator : AbstractValidator<RegisterParcelRequest>
{
    private static readonly Regex HsCodePattern =
        new(@"^\d{4}\.\d{2}$", RegexOptions.Compiled);

    public RegisterParcelRequestValidator()
    {
        RuleFor(x => x.ShipperAddressId).GreaterThan(0);
        RuleFor(x => x.RecipientAddressId).GreaterThan(0);

        RuleFor(x => x.ServiceType)
            .NotEmpty()
            .Must(v => Enum.TryParse<ServiceType>(v, ignoreCase: true, out _))
            .WithMessage("ServiceType must be one of: Economy, Standard, Express, Overnight.");

        RuleFor(x => x.Weight).NotNull();
        When(x => x.Weight is not null, () =>
        {
            RuleFor(x => x.Weight.Value).GreaterThan(0);
            RuleFor(x => x.Weight.Unit)
                .Must(u => Enum.TryParse<WeightUnit>(u, ignoreCase: true, out _))
                .WithMessage("Weight unit must be 'kg' or 'lb'.");
        });

        RuleFor(x => x.Dimensions).NotNull();
        When(x => x.Dimensions is not null, () =>
        {
            RuleFor(x => x.Dimensions.Length).GreaterThan(0);
            RuleFor(x => x.Dimensions.Width).GreaterThan(0);
            RuleFor(x => x.Dimensions.Height).GreaterThan(0);
            RuleFor(x => x.Dimensions.Unit)
                .Must(u => Enum.TryParse<DimensionUnit>(u, ignoreCase: true, out _))
                .WithMessage("Dimension unit must be 'cm' or 'in'.");
        });

        RuleFor(x => x.DeclaredValue).NotNull();
        When(x => x.DeclaredValue is not null, () =>
        {
            RuleFor(x => x.DeclaredValue.Amount).GreaterThan(0);
            RuleFor(x => x.DeclaredValue.Currency)
                .Length(3)
                .WithMessage("Currency must be a 3-letter ISO 4217 code.");
        });

        RuleFor(x => x.ContentItems)
            .NotEmpty()
            .WithMessage("At least one content item is required for customs compliance.");

        RuleForEach(x => x.ContentItems).ChildRules(item =>
        {
            item.RuleFor(ci => ci.HsCode)
                .NotEmpty()
                .Matches(HsCodePattern)
                .WithMessage("HS code must follow the format XXXX.XX (e.g., 8471.30).");

            item.RuleFor(ci => ci.Description).NotEmpty();
            item.RuleFor(ci => ci.Quantity).GreaterThan(0);
            item.RuleFor(ci => ci.UnitValue).GreaterThan(0);

            item.RuleFor(ci => ci.CountryOfOrigin)
                .Length(2)
                .WithMessage("Country of origin must be a 2-letter ISO 3166-1 alpha-2 code.");
        });
    }
}
