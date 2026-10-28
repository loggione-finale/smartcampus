using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public class AcceptInvitationResult
{
    public AcceptInvitationOutcome Outcome { get; private init; }
    public Invitation? Invitation { get; private init; }

    private AcceptInvitationResult() { }

    public static AcceptInvitationResult Accepted(Invitation invitation) =>
        new() { Outcome = AcceptInvitationOutcome.Accepted, Invitation = invitation };

    public static AcceptInvitationResult Failed(AcceptInvitationOutcome outcome) =>
        new() { Outcome = outcome };
}
