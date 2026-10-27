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
    DateTime? RespondedAt)
{
    public static InvitationResponse From(Invitation invitation) => new(
        invitation.Id,
        invitation.BookingId,
        invitation.Email,
        invitation.UserId,
        invitation.Role,
        invitation.Status,
        invitation.ExpiresAt,
        invitation.RespondedAt);
}
