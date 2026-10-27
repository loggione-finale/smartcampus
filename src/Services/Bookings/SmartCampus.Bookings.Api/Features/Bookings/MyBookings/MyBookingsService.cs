using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.MyBookings;

public class MyBookingsService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<List<MyBookingResponse>> GetMineAsync(
        Guid userId, string? email, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The organizer has an Invitation row with Role = Organizer, so the
        // invitations table is the single source for "my bookings": a booking
        // is mine when I hold one of its invitations. Anonymous invitations
        // (UserId = null) match by email so a user who registered later still
        // sees the bookings they were invited to.
        var invitations = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Booking)
            .ThenInclude(b => b.ClosingComment)
            .Where(i => i.UserId == userId
                || (i.UserId == null && email != null && i.Email == email))
            .ToListAsync(cancellationToken);

        // Future first (soonest ahead), then past (most recent first).
        var futureFirst = invitations
            .Where(i => i.Period.UpperBound >= now)
            .OrderBy(i => i.Period.LowerBound)
            .Concat(invitations
                .Where(i => i.Period.UpperBound < now)
                .OrderByDescending(i => i.Period.LowerBound))
            .ToList();

        return futureFirst
            .Select(i => MyBookingResponse.From(
                i.Booking,
                i.Role,
                i.Status == InvitationStatus.Invited ? i.ExpiresAt : null,
                now))
            .ToList();
    }
}
