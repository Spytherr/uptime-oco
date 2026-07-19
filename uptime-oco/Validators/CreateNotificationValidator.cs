using FluentValidation;

namespace uptime_oco;

public class CreateNotificationValidator : AbstractValidator<CreateNotificationViewModel>
{
    public CreateNotificationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid notification type.");

        RuleFor(x => x.Target)
            .NotEmpty().WithMessage("Webhook URL is required.")
            .Must(BeValidUrl).WithMessage("Must be a valid HTTPS URL.");
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}
