using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public enum StatsGroupBy
{
    Space,
    Type,
    Building
}

public record StatsGroupResponse(
    string Key,
    string Label,
    int TotalBookings,
    int Confirmed,
    int Cancelled,
    int Closed,
    int SpaceUsed,
    int SpaceNotUsed,
    int TotalAttendees,
    int IssuesReported);

public class AdminStatsService(BookingsDbContext dbContext)
{
    // Both dates are inclusive and refer to the campus booking date.
    public async Task<List<StatsGroupResponse>> GetAsync(
        DateOnly from, DateOnly to, StatsGroupBy groupBy, CancellationToken cancellationToken)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.BookingDate >= from && b.BookingDate <= to)
            .Select(b => new
            {
                b.SpaceId,
                b.SpaceNameSnapshot,
                b.SpaceTypeSnapshot,
                b.SpaceBuildingSnapshot,
                b.Status,
                Comment = b.ClosingComment
            })
            .ToListAsync(cancellationToken);

        return bookings
            .GroupBy(b => groupBy switch
            {
                StatsGroupBy.Type => (Key: b.SpaceTypeSnapshot, Label: b.SpaceTypeSnapshot),
                StatsGroupBy.Building => (Key: b.SpaceBuildingSnapshot, Label: b.SpaceBuildingSnapshot),
                _ => (Key: b.SpaceId.ToString(), Label: b.SpaceNameSnapshot)
            })
            .Select(group => new StatsGroupResponse(
                group.Key.Key,
                group.Key.Label,
                group.Count(),
                group.Count(b => b.Status == BookingStatus.Confirmed),
                group.Count(b => b.Status == BookingStatus.Cancelled),
                group.Count(b => b.Status == BookingStatus.Closed),
                group.Count(b => b.Comment is { SpaceUsed: true }),
                group.Count(b => b.Comment is { SpaceUsed: false }),
                group.Sum(b => b.Comment?.AttendeesCount ?? 0),
                group.Count(b => b.Comment is { IssuesReported: true })))
            .OrderBy(s => s.Label)
            .ToList();
    }
}
