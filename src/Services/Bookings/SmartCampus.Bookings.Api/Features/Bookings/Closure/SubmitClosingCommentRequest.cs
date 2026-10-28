namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public record SubmitClosingCommentRequest(
    bool? SpaceUsed,
    int? AttendeesCount,
    bool? IssuesReported,
    string? Comment);
