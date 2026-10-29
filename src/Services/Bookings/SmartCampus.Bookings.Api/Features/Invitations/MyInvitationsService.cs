using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public class MyInvitationsService(BookingsDbContext dbContext)
{
    public async Task<List<InvitationResponse>> GetMineAsync(
        Guid userId, string? email, CancellationToken cancellationToken)
    {
        var invitations = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Booking)
            .Where(i => (i.UserId == userId
                    || (i.UserId == null && email != null
                        && email.ToLower() == i.Email!.ToLower()))
                && i.Role == ParticipantRole.Participant)
            .OrderBy(i => i.Period.LowerBound)
            .ToListAsync(cancellationToken);

        // Participant counts per booking, computed separately: including
        // Booking.Invitations together with the Booking navigation would form
        // a cycle, which no-tracking queries reject.
        var bookingIds = invitations.Select(i => i.BookingId).Distinct().ToList();
        var participantCounts = await dbContext.Invitations
            .AsNoTracking()
            .Where(i => bookingIds.Contains(i.BookingId)
                && i.Role == ParticipantRole.Participant
                && (i.Status == InvitationStatus.Invited || i.Status == InvitationStatus.Accepted))
            .GroupBy(i => i.BookingId)
            .Select(g => new { BookingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BookingId, x => x.Count, cancellationToken);

        // Pending and expired invitations stay visible so the user can see they
        // were excluded from the booking after the deadline; declined, cancelled
        // and left ones disappear.
        return invitations
            .Where(i => i.Status
                is InvitationStatus.Invited
                or InvitationStatus.Accepted
                or InvitationStatus.Expired)
            .Select(i => new
            {
                Invitation = i,
                ParticipantCount = participantCounts.TryGetValue(i.BookingId, out var count) ? count : 0,
            })
            .Select(x => InvitationResponse.From(x.Invitation, x.ParticipantCount))
            .ToList();
    }
}
