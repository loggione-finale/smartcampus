using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Admin;

public class AdminCancelBookingService(
    BookingsDbContext dbContext, INotificationSender notificationSender, TimeProvider timeProvider)
{
    public async Task<CancelBookingResult> CancelAsync(
        string referenceCode, Guid adminId, string reason, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.Invitations)
            .SingleOrDefaultAsync(b => b.ReferenceCode == referenceCode, cancellationToken);

        if (booking is null)
        {
            return CancelBookingResult.Failed(CancelBookingOutcome.NotFound);
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
        booking.CancelledBy = adminId;
        booking.CancellationReason = reason.Trim();
        booking.UpdatedAt = now;

        var subject = $"Prenotazione {booking.ReferenceCode} annullata";
        var body = $"La prenotazione di {booking.SpaceNameSnapshot} del " +
                   $"{booking.Period.LowerBound:dd/MM/yyyy HH:mm} è stata annullata dall'amministrazione. " +
                   $"Motivo: {booking.CancellationReason}";

        foreach (var invitation in booking.Invitations
                     .Where(i => i.Status is InvitationStatus.Invited or InvitationStatus.Accepted))
        {
            invitation.Status = InvitationStatus.Cancelled;
            invitation.RespondedAt ??= now;
            notificationSender.QueueEmail(invitation.Email, subject, body, "booking-cancelled", booking.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CancelBookingResult.Cancelled(booking);
    }
}
