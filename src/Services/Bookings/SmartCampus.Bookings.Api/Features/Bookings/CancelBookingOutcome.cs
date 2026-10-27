namespace SmartCampus.Bookings.Api.Features.Bookings;

public enum CancelBookingOutcome
{
    Cancelled,
    NotFound,
    Forbidden,
    AlreadyCancelled,
    AlreadyEnded
}
