using FluentValidation;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;

namespace ParcelTracking.Application.Validators;

public class CreateTrackingEventRequestValidator : AbstractValidator<CreateTrackingEventRequest>
{
    public CreateTrackingEventRequestValidator()
    {
        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required.");

        RuleFor(x => x.EventType)
            .IsInEnum()
            .WithMessage("EventType must be a valid event type.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.LocationCity)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.LocationCity))
            .WithMessage("LocationCity must not exceed 100 characters.");

        RuleFor(x => x.LocationState)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.LocationState))
            .WithMessage("LocationState must not exceed 100 characters.");

        RuleFor(x => x.LocationCountry)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.LocationCountry))
            .WithMessage("LocationCountry must not exceed 100 characters.");

        RuleFor(x => x.DelayReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.DelayReason))
            .WithMessage("DelayReason must not exceed 500 characters.");
    }
}
