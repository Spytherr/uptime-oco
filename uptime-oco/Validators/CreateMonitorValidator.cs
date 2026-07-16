using FluentValidation;

namespace uptime_oco;

public class CreateMonitorValidator : AbstractValidator<CreateMonitorViewModel>
{
    public CreateMonitorValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .Must(BeValidUrl).WithMessage("Must be a valid HTTP or HTTPS URL.");

        RuleFor(x => x.IntervalSeconds)
            .InclusiveBetween(10, 3600).WithMessage("Interval must be between 10 and 3600 seconds.");

        RuleFor(x => x.RetryThreshold)
            .InclusiveBetween(1, 10).WithMessage("Retry threshold must be between 1 and 10.");
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
