using System.ComponentModel.DataAnnotations;

namespace SmartCampus.Spaces.Api.Features.Unavailabilities.Models;

public enum DeleteUnavailabilityResult
{
    Deleted,
    SpaceNotFound,
    UnavailabilityNotFound
}

public sealed record ValidatedUnavailabilityInput(
    DateTime StartUtc,
    DateTime EndUtc,
    string Reason);

public sealed class CreateUnavailabilityRequest
{
    public const int MaxReasonLength = 500;

    [Required]
    public DateTimeOffset? Start { get; init; }

    [Required]
    public DateTimeOffset? End { get; init; }

    [Required]
    [MaxLength(MaxReasonLength)]
    public string? Reason { get; init; }

    public bool TryValidateAndNormalize(
        out ValidatedUnavailabilityInput? validated,
        out string? errorDetail)
    {
        validated = null;

        if (!Start.HasValue || !End.HasValue || Start.Value == default || End.Value == default)
        {
            errorDetail = "Both 'start' and 'end' timestamps are required.";
            return false;
        }

        var startUtc = DateTime.SpecifyKind(Start.Value.UtcDateTime, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(End.Value.UtcDateTime, DateTimeKind.Utc);

        if (endUtc <= startUtc)
        {
            errorDetail = "The 'end' timestamp must be strictly after 'start'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            errorDetail = "The 'reason' field is required.";
            return false;
        }

        var trimmedReason = Reason.Trim();
        if (trimmedReason.Length > MaxReasonLength)
        {
            errorDetail = $"The 'reason' field cannot exceed {MaxReasonLength} characters.";
            return false;
        }

        if (trimmedReason.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')))
        {
            errorDetail = "The 'reason' field contains invalid control characters.";
            return false;
        }

        validated = new ValidatedUnavailabilityInput(startUtc, endUtc, trimmedReason);
        errorDetail = null;
        return true;
    }
}

public sealed class UnavailabilityListQuery
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public bool TryNormalize(
        out (DateTime FromUtc, DateTime ToUtc)? periodFilter,
        out string? errorDetail)
    {
        periodFilter = null;
        errorDetail = null;

        if (!From.HasValue && !To.HasValue)
        {
            return true;
        }

        if (!From.HasValue || !To.HasValue)
        {
            errorDetail = "Both 'from' and 'to' query parameters must be provided together when filtering by period.";
            return false;
        }

        var fromUtc = DateTime.SpecifyKind(From.Value.UtcDateTime, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(To.Value.UtcDateTime, DateTimeKind.Utc);

        if (toUtc <= fromUtc)
        {
            errorDetail = "The 'to' parameter must be strictly after 'from'.";
            return false;
        }

        periodFilter = (fromUtc, toUtc);
        return true;
    }
}
