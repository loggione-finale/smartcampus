using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class CancelBookingService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<CancelBookingResult> CancelAsync(
        Guid bookingId, Guid userId, string? reason, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
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

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var cancelled = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var affected = await dbContext.Bookings
                .Where(b => b.Id == bookingId && b.Status == BookingStatus.Confirmed)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(b => b.Status, BookingStatus.Cancelled)
                        .SetProperty(b => b.CancelledAt, now)
                        .SetProperty(b => b.CancelledBy, userId)
                        .SetProperty(b => b.CancellationReason, trimmedReason)
                        .SetProperty(b => b.UpdatedAt, now),
                    cancellationToken);

            if (affected == 0)
            {
                return false;
            }

            await dbContext.Invitations
                .Where(i => i.BookingId == bookingId
                    && (i.Status == InvitationStatus.Invited || i.Status == InvitationStatus.Accepted))
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(i => i.Status, InvitationStatus.Cancelled)
                        .SetProperty(i => i.RespondedAt, i => i.RespondedAt ?? now),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        });

        var current = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Invitations)
            .SingleAsync(b => b.Id == bookingId, cancellationToken);

        if (cancelled)
        {
            return CancelBookingResult.Cancelled(current);
        }

        return CancelBookingResult.Failed(current.Status == BookingStatus.Cancelled
            ? CancelBookingOutcome.AlreadyCancelled
            : CancelBookingOutcome.AlreadyEnded);
    }
}
