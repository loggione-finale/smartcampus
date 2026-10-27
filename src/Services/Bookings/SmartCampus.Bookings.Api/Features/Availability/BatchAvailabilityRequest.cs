namespace SmartCampus.Bookings.Api.Features.Availability;

public record BatchAvailabilityRequest
{
    public IReadOnlyList<Guid>? SpaceIds { get; init; }
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    public DateTimeOffset? EffectiveStart => Start ?? From;
    public DateTimeOffset? EffectiveEnd => End ?? To;
}
