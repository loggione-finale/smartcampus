namespace SmartCampus.Bookings.Api.Features.Availability;

public class AvailabilityResult
{
    public AvailabilityOutcome Outcome { get; }
    public SpaceAvailabilityResponse? Response { get; }
    public string? Detail { get; }

    private AvailabilityResult(AvailabilityOutcome outcome, SpaceAvailabilityResponse? response, string? detail)
    {
        Outcome = outcome;
        Response = response;
        Detail = detail;
    }

    public static AvailabilityResult Success(SpaceAvailabilityResponse response) =>
        new(AvailabilityOutcome.Success, response, null);

    public static AvailabilityResult SpaceNotFound(string detail = "The requested space was not found.") =>
        new(AvailabilityOutcome.SpaceNotFound, null, detail);

    public static AvailabilityResult SpacesUnavailable(string detail = "The Spaces service is unreachable.") =>
        new(AvailabilityOutcome.SpacesUnavailable, null, detail);
}
