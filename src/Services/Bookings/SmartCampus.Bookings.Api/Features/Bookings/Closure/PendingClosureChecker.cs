using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public class PendingClosureChecker(BookingsDbContext dbContext, TimeProvider timeProvider) : IPendingClosureChecker
{
    public async Task<string?> GetPendingReferenceCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return await dbContext.Bookings
            .Where(b => b.OrganizerId == userId
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.AwaitingComment)
                && b.Period.UpperBound <= now
                && b.ClosingComment == null)
            .OrderBy(b => b.Period.UpperBound)
            .Select(b => b.ReferenceCode)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
