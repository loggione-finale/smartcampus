using System.Text.Json.Serialization;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public record ClosingCommentResponse(
    Guid Id,
    Guid BookingId,
    string ReferenceCode,
    Guid AuthorId,
    bool SpaceUsed,
    int AttendeesCount,
    bool IssuesReported,
    string Comment,
    DateTime CreatedAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    BookingStatus BookingStatus)
{
    public static ClosingCommentResponse From(ClosingComment comment, Booking booking) => new(
        comment.Id,
        comment.BookingId,
        booking.ReferenceCode,
        comment.AuthorId,
        comment.SpaceUsed,
        comment.AttendeesCount,
        comment.IssuesReported,
        comment.Comment,
        comment.CreatedAt,
        booking.Status);
}
