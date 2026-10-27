using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public record BookingResponse(
    Guid Id,
    string ReferenceCode,
    Guid SpaceId,
    string SpaceName,
    string SpaceType,
    string SpaceBuilding,
    Guid OrganizerId,
    DateTime Start,
    DateTime End,
    BookingStatus Status,
    DateTime CreatedAt)
{
    public static BookingResponse From(Booking booking, DateTime? now = null)
    {
        var status = booking.Status;
        if (status == BookingStatus.Confirmed && now.HasValue && booking.Period.UpperBound <= now.Value && booking.ClosingComment == null)
        {
            status = BookingStatus.AwaitingComment;
        }

        return new(
            booking.Id,
            booking.ReferenceCode,
            booking.SpaceId,
            booking.SpaceNameSnapshot,
            booking.SpaceTypeSnapshot,
            booking.SpaceBuildingSnapshot,
            booking.OrganizerId,
            booking.Period.LowerBound,
            booking.Period.UpperBound,
            status,
            booking.CreatedAt);
    }
}
