using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;

namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public class ClosingCommentService(
    BookingsDbContext dbContext,
    IAdminAlerts adminAlerts,
    ClosingCommentValidator validator,
    TimeProvider timeProvider) : IClosingCommentService
{
    public async Task<ClosingCommentResult> SubmitAsync(
        string code,
        Guid userId,
        SubmitClosingCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var booking = Guid.TryParse(code, out var guidId)
            ? await dbContext.Bookings
                .Include(b => b.ClosingComment)
                .FirstOrDefaultAsync(b => b.Id == guidId, cancellationToken)
            : await dbContext.Bookings
                .Include(b => b.ClosingComment)
                .FirstOrDefaultAsync(b => b.ReferenceCode == code, cancellationToken);

        if (booking is null || booking.OrganizerId != userId)
        {
            return ClosingCommentResult.NotFound($"No booking found with reference code or id '{code}' for the current user.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return ClosingCommentResult.AlreadyCancelled("A cancelled booking cannot be closed with a comment.");
        }

        if (booking.Status == BookingStatus.Closed || booking.ClosingComment is not null)
        {
            return ClosingCommentResult.AlreadyClosed("A closing comment has already been submitted for this booking.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (booking.Period.UpperBound > now)
        {
            return ClosingCommentResult.NotEnded("A closing comment can only be submitted after the booking has ended.");
        }

        var validationErrors = validator.Validate(request, booking.SpaceCapacitySnapshot);
        if (validationErrors.Count > 0)
        {
            return ClosingCommentResult.Invalid(validationErrors);
        }

        var closingComment = new ClosingComment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            AuthorId = userId,
            SpaceUsed = request.SpaceUsed,
            AttendeesCount = request.SpaceUsed ? request.AttendeesCount : 0,
            IssuesReported = request.IssuesReported,
            Comment = request.Comment!.Trim(),
            CreatedAt = now
        };

        dbContext.ClosingComments.Add(closingComment);
        booking.ClosingComment = closingComment;
        booking.Status = BookingStatus.Closed;
        booking.UpdatedAt = now;

        if (request.IssuesReported)
        {
            await adminAlerts.RaiseAsync(
                AdminAlertKind.ReportedIssue,
                booking.Id,
                $"Problema segnalato nel commento di {booking.ReferenceCode}: {closingComment.Comment}",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ClosingCommentResult.Success(closingComment, booking);
    }
}
