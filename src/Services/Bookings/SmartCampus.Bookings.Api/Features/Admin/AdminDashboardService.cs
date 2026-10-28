using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public class AdminDashboardService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<List<DashboardSpaceResponse>> GetAsync(
        DateOnly? date, string? building, string? type, CancellationToken cancellationToken)
    {
        var day = date ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, BookingTimeBands.CampusTimeZone));

        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Invitations)
            .Where(b => b.BookingDate == day);

        if (!string.IsNullOrWhiteSpace(building))
        {
            query = query.Where(b => b.SpaceBuildingSnapshot == building);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(b => b.SpaceTypeSnapshot == type);
        }

        var bookings = await query.ToListAsync(cancellationToken);

        return bookings
            .GroupBy(b => b.SpaceId)
            .Select(group =>
            {
                var first = group.First();
                return new DashboardSpaceResponse(
                    first.SpaceId,
                    first.SpaceNameSnapshot,
                    first.SpaceTypeSnapshot,
                    first.SpaceBuildingSnapshot,
                    first.SpaceCapacitySnapshot,
                    group.OrderBy(b => b.Period.LowerBound).Select(ToResponse).ToList());
            })
            .OrderBy(s => s.SpaceBuilding)
            .ThenBy(s => s.SpaceName)
            .ToList();
    }

    private static DashboardBookingResponse ToResponse(Booking booking) => new(
        booking.ReferenceCode,
        booking.OrganizerNameSnapshot,
        booking.Period.LowerBound,
        booking.Period.UpperBound,
        booking.Status,
        booking.Status == BookingStatus.Cancelled
            ? 0
            : booking.Invitations.Count(i => i.Status is InvitationStatus.Invited or InvitationStatus.Accepted),
        booking.Invitations
            .OrderBy(i => i.Role)
            .ThenBy(i => i.Email)
            .Select(i => new DashboardParticipantResponse(i.Email, i.DisplayNameSnapshot, i.Role, i.Status))
            .ToList());
}
