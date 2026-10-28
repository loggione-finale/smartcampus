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
    int WithoutComment,
    int TotalAttendees,
    int IssuesReported);

public class AdminStatsService(BookingsDbContext dbContext, TimeProvider timeProvider)
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
                b.Period,
                Comment = b.ClosingComment
            })
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Space groups use the id only: the label is the name snapshot of the latest booking in the period.
        return bookings
            .GroupBy(b => groupBy switch
            {
                StatsGroupBy.Type => b.SpaceTypeSnapshot,
                StatsGroupBy.Building => b.SpaceBuildingSnapshot,
                _ => b.SpaceId.ToString()
            })
            .Select(group => new StatsGroupResponse(
                group.Key,
                groupBy == StatsGroupBy.Space
                    ? group.MaxBy(b => b.Period.LowerBound)!.SpaceNameSnapshot
                    : group.Key,
                group.Count(),
                group.Count(b => b.Status == BookingStatus.Confirmed),
                group.Count(b => b.Status == BookingStatus.Cancelled),
                group.Count(b => b.Status == BookingStatus.Closed),
                group.Count(b => b.Comment is { SpaceUsed: true }),
                group.Count(b => b.Comment is { SpaceUsed: false }),
                // Finished, not cancelled, no closing comment.
                group.Count(b => b.Status != BookingStatus.Cancelled && b.Period.UpperBound <= now && b.Comment is null),
                group.Sum(b => b.Comment?.AttendeesCount ?? 0),
                group.Count(b => b.Comment is { IssuesReported: true })))
            .OrderBy(s => s.Label)
            .ToList();
    }
}
