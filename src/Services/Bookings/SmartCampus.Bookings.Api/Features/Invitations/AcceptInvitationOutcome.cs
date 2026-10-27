namespace SmartCampus.Bookings.Api.Features.Invitations;

public enum AcceptInvitationOutcome
{
    Accepted,
    NotFound,
    Forbidden,
    Expired,
    PersonConflict
}
