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
        Booking booking, ParticipantRole role, DateTime? invitationExpiresAt, DateTime now)
    {
        var hasPendingClosure = (booking.Status is BookingStatus.Confirmed or BookingStatus.AwaitingComment)
            && booking.Period.UpperBound <= now
            && booking.ClosingComment == null;

        var status = hasPendingClosure && booking.Status == BookingStatus.Confirmed
            ? BookingStatus.AwaitingComment
            : booking.Status;

        return new(
            booking.Id,
            booking.ReferenceCode,
            booking.SpaceId,
            booking.SpaceNameSnapshot,
            booking.SpaceBuildingSnapshot,
            booking.Period.LowerBound,
            booking.Period.UpperBound,
            status,
            role,
            HasPendingClosureComment: hasPendingClosure,
            HasPendingInvitation: invitationExpiresAt is not null,
            InvitationExpiresAt: invitationExpiresAt);
    }
}
