using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public class ClosingCommentResult
{
    public ClosingCommentOutcome Outcome { get; init; }
    public ClosingComment? Comment { get; init; }
    public Booking? Booking { get; init; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
    public string? Detail { get; init; }

    public static ClosingCommentResult Success(ClosingComment comment, Booking booking) => new()
    {
        Outcome = ClosingCommentOutcome.Success,
        Comment = comment,
        Booking = booking
    };

    public static ClosingCommentResult NotFound(string detail) => new()
    {
        Outcome = ClosingCommentOutcome.NotFound,
        Detail = detail
    };

    public static ClosingCommentResult NotEnded(string detail) => new()
    {
        Outcome = ClosingCommentOutcome.NotEnded,
        Detail = detail
    };

    public static ClosingCommentResult AlreadyCancelled(string detail) => new()
    {
        Outcome = ClosingCommentOutcome.AlreadyCancelled,
        Detail = detail
    };

    public static ClosingCommentResult AlreadyClosed(string detail) => new()
    {
        Outcome = ClosingCommentOutcome.AlreadyClosed,
        Detail = detail
    };

    public static ClosingCommentResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new()
    {
        Outcome = ClosingCommentOutcome.Invalid,
        ValidationErrors = errors
    };
}
