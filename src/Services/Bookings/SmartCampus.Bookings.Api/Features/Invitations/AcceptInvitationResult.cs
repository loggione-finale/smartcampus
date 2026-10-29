using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public class AcceptInvitationResult
{
    public AcceptInvitationOutcome Outcome { get; private init; }
    public Invitation? Invitation { get; private init; }
    public int ParticipantCount { get; private init; }

    private AcceptInvitationResult() { }

    public static AcceptInvitationResult Accepted(Invitation invitation, int participantCount) =>
        new() { Outcome = AcceptInvitationOutcome.Accepted, Invitation = invitation, ParticipantCount = participantCount };

    public static AcceptInvitationResult Failed(AcceptInvitationOutcome outcome) =>
        new() { Outcome = outcome };
}
