using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Api.Features.Catalog.Models;

public sealed record UnavailabilityResponse(
    string Id,
    DateTime Start,
    DateTime End,
    string Reason)
{
    public static UnavailabilityResponse From(Unavailability unavailability) =>
        new(
            Id: unavailability.Id,
            Start: unavailability.Start,
            End: unavailability.End,
            Reason: unavailability.Reason);
}
