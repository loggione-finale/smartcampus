using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class CancelBookingService(BookingsDbContext dbContext, TimeProvider timeProvider, IEmailOutbox emailOutbox)
{
    public async Task<CancelBookingResult> CancelAsync(
        Guid bookingId, Guid userId, string? reason, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Invitations)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return CancelBookingResult.Failed(CancelBookingOutcome.NotFound);
        }

        if (booking.OrganizerId != userId)
        {
            return CancelBookingResult.Failed(CancelBookingOutcome.Forbidden);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return CancelBookingResult.Failed(CancelBookingOutcome.AlreadyCancelled);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (booking.Status == BookingStatus.Closed || booking.Period.UpperBound <= now)
        {
            return CancelBookingResult.Failed(CancelBookingOutcome.AlreadyEnded);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancelledBy = userId;
        booking.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        booking.UpdatedAt = now;

        foreach (var invitation in booking.Invitations
                     .Where(i => i.Status is InvitationStatus.Invited or InvitationStatus.Accepted))
        {
            invitation.Status = InvitationStatus.Cancelled;
            invitation.RespondedAt ??= now;
        }

        // Le code delle mail vengono salvate insieme all'annullamento:
        // la scrittura dei .eml avviene nel dispatcher in background.
        emailOutbox.EnqueueBookingCancelledEmails(booking);

        await dbContext.SaveChangesAsync(cancellationToken);

        return CancelBookingResult.Cancelled(booking);
    }
}
