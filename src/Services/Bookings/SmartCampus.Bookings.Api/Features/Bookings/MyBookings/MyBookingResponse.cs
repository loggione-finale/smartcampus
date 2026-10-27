using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.MyBookings;

public record MyBookingResponse(
    Guid Id,
    string ReferenceCode,
    Guid SpaceId,
    string SpaceName,
    string SpaceBuilding,
    DateTime Start,
    DateTime End,
    BookingStatus Status,
    ParticipantRole Role,
    bool HasPendingClosureComment,
    bool HasPendingInvitation,
    DateTime? InvitationExpiresAt)
{
    public static MyBookingResponse From(
        Booking booking, ParticipantRole role, DateTime? invitationExpiresAt, DateTime now) => new(
        booking.Id,
        booking.ReferenceCode,
        booking.SpaceId,
        booking.SpaceNameSnapshot,
        booking.SpaceBuildingSnapshot,
        booking.Period.LowerBound,
        booking.Period.UpperBound,
        booking.Status,
        role,
        // "Concluded without comment" per SC-08: ended but still confirmed,
        // i.e. the organizer has not filled the closing comment yet.
        HasPendingClosureComment: booking.Status == BookingStatus.Confirmed
            && booking.Period.UpperBound <= now
            && booking.ClosingComment == null,
        HasPendingInvitation: invitationExpiresAt is not null,
        InvitationExpiresAt: invitationExpiresAt);
}
