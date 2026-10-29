using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Invitations;

public record InvitationResponse(
    Guid Id,
    Guid BookingId,
    string Email,
    Guid? UserId,
    ParticipantRole Role,
    InvitationStatus Status,
    DateTime ExpiresAt,
    DateTime? RespondedAt,
    string SpaceName,
    string SpaceBuilding,
    DateTime Start,
    DateTime End,
    int ParticipantCount)
{
    public static InvitationResponse From(Invitation invitation) => new(
        invitation.Id,
        invitation.BookingId,
        invitation.Email,
        invitation.UserId,
        invitation.Role,
        invitation.Status,
        invitation.ExpiresAt,
        invitation.RespondedAt,
        invitation.Booking.SpaceNameSnapshot,
        invitation.Booking.SpaceBuildingSnapshot,
        invitation.Booking.Period.LowerBound,
        invitation.Booking.Period.UpperBound,
        invitation.Booking.Invitations.Count(i =>
            i.Role == ParticipantRole.Participant
            && i.Status is InvitationStatus.Invited or InvitationStatus.Accepted));

    public static InvitationResponse From(Invitation invitation, int participantCount) => new(
        invitation.Id,
        invitation.BookingId,
        invitation.Email,
        invitation.UserId,
        invitation.Role,
        invitation.Status,
        invitation.ExpiresAt,
        invitation.RespondedAt,
        invitation.Booking.SpaceNameSnapshot,
        invitation.Booking.SpaceBuildingSnapshot,
        invitation.Booking.Period.LowerBound,
        invitation.Booking.Period.UpperBound,
        participantCount);
}
