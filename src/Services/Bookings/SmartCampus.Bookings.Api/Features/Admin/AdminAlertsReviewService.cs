using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public class AdminAlertsReviewService(BookingsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<List<AdminAlertResponse>> ListAsync(AdminAlertStatus? status, CancellationToken cancellationToken)
    {
        var query = dbContext.AdminAlerts.AsNoTracking().Include(a => a.Booking).AsQueryable();
        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        var alerts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        return alerts.Select(AdminAlertResponse.From).ToList();
    }

    public async Task<(AdminAlertResponse? Alert, bool AlreadyHandled)> ResolveAsync(
        Guid id, Guid adminId, string? note, CancellationToken cancellationToken)
    {
        var alert = await dbContext.AdminAlerts.Include(a => a.Booking).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (alert is null)
        {
            return (null, false);
        }

        if (alert.Status == AdminAlertStatus.Handled)
        {
            return (AdminAlertResponse.From(alert), true);
        }

        alert.Status = AdminAlertStatus.Handled;
        alert.HandledBy = adminId;
        alert.HandledAt = timeProvider.GetUtcNow().UtcDateTime;
        alert.HandlingNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return (AdminAlertResponse.From(alert), false);
    }
}
