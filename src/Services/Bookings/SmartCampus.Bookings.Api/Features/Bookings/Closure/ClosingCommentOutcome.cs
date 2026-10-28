namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public enum ClosingCommentOutcome
{
    Success,
    NotFound,
    NotEnded,
    AlreadyCancelled,
    AlreadyClosed,
    Invalid
}
