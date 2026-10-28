using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    private const string ClosingCommentUniqueIndex = "ux_closing_comments_booking";

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

        var isSpaceUsed = request.SpaceUsed == true;
        var attendees = isSpaceUsed ? (request.AttendeesCount ?? 0) : 0;
        var issuesReported = request.IssuesReported == true;

        var closingComment = new ClosingComment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            AuthorId = userId,
            SpaceUsed = isSpaceUsed,
            AttendeesCount = attendees,
            IssuesReported = issuesReported,
            Comment = request.Comment!.Trim(),
            CreatedAt = now
        };

        try
        {
            dbContext.ClosingComments.Add(closingComment);
            booking.ClosingComment = closingComment;
            booking.Status = BookingStatus.Closed;
            booking.UpdatedAt = now;

            if (issuesReported)
            {
                await adminAlerts.RaiseAsync(
                    AdminAlertKind.ReportedIssue,
                    booking.Id,
                    $"Problema segnalato nel commento di {booking.ReferenceCode}: {closingComment.Comment}",
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsClosingCommentUniqueViolation(ex))
        {
            dbContext.ChangeTracker.Clear();
            return ClosingCommentResult.AlreadyClosed("A closing comment has already been submitted for this booking.");
        }

        return ClosingCommentResult.Success(closingComment, booking);
    }

    private static bool IsClosingCommentUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgres &&
               postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
               postgres.ConstraintName == ClosingCommentUniqueIndex;
    }
}
