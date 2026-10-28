using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public class MyInvitationsService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<List<InvitationResponse>> GetMineAsync(
        Guid userId, string? email, CancellationToken cancellationToken)
    {
        var invitations = await dbContext.Invitations
            .AsNoTracking()
            .Where(i => (i.UserId == userId
                    || (i.UserId == null && email != null
                        && email.ToLower() == i.Email!.ToLower()))
                && i.Role == ParticipantRole.Participant)
            .OrderBy(i => i.Period.LowerBound)
            .ToListAsync(cancellationToken);

        // Pending invitations stay visible after the deadline so the user can
        // see they were excluded; declined, cancelled and left ones disappear.
        return invitations
            .Where(i => i.Status is InvitationStatus.Invited or InvitationStatus.Accepted)
            .Select(InvitationResponse.From)
            .ToList();
    }
}
