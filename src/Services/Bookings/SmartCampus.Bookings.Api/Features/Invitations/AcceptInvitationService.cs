using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public class AcceptInvitationService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    private const string PersonOverlapConstraint = "ex_invitations_person_no_overlap";

    public async Task<AcceptInvitationResult> AcceptAsync(
        Guid invitationId, Guid userId, string email, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations
            .Include(i => i.Booking)
            .SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null
            || invitation.Booking.Status == BookingStatus.Cancelled
            || (invitation.UserId is null
                && !string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return AcceptInvitationResult.Failed(AcceptInvitationOutcome.NotFound);
        }

        // Only the person who registered with the invited email can accept it.
        if (invitation.UserId is not null && invitation.UserId != userId)
        {
            return AcceptInvitationResult.Failed(AcceptInvitationOutcome.Forbidden);
        }

        if (invitation.Status != InvitationStatus.Invited || invitation.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return AcceptInvitationResult.Failed(AcceptInvitationOutcome.Expired);
        }

        var bandAlreadyUsed = await dbContext.Invitations.AnyAsync(
            i => i.UserId == userId
                && i.BookingId != invitation.BookingId
                && i.Status == InvitationStatus.Accepted
                && i.Booking.Status == BookingStatus.Confirmed
                && i.Booking.BookingDate == invitation.Booking.BookingDate
                && i.Booking.TimeBand == invitation.Booking.TimeBand,
            cancellationToken);
        if (bandAlreadyUsed)
        {
            return AcceptInvitationResult.Failed(AcceptInvitationOutcome.TimeBandAlreadyUsed);
        }

        invitation.UserId ??= userId;
        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPersonOverlapConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            return AcceptInvitationResult.Failed(AcceptInvitationOutcome.PersonConflict);
        }

        // The response includes booking details and the participant count:
        // load the booking (already tracked above) and its invitations.
        await dbContext.Entry(invitation.Booking).Collection(b => b.Invitations).LoadAsync(cancellationToken);
        var participantCount = invitation.Booking.Invitations.Count(i =>
            i.Role == ParticipantRole.Participant
            && i.Status is InvitationStatus.Invited or InvitationStatus.Accepted);

        return AcceptInvitationResult.Accepted(invitation, participantCount);
    }

    private static bool IsPersonOverlapConflict(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException postgres
            && postgres.ConstraintName == PersonOverlapConstraint;
}
