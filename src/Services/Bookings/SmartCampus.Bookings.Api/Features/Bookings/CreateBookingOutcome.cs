namespace SmartCampus.Bookings.Api.Features.Bookings;

public enum CreateBookingOutcome
{
    Created,
    Invalid,
    PendingClosure,
    SpaceNotFound,
    SpaceNotBookable,
    SpaceConflict,
    PersonConflict,
    SpacesUnavailable
}
