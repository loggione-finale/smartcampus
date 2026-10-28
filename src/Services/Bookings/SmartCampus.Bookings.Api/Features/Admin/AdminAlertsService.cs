using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public class AdminAlertsService(BookingsDbContext dbContext, TimeProvider timeProvider) : IAdminAlerts
{
    public async Task RaiseAsync(AdminAlertKind kind, Guid bookingId, string message, CancellationToken cancellationToken = default)
    {
        var alert = new AdminAlert
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            BookingId = bookingId,
            Message = message,
            Status = AdminAlertStatus.Open,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.AdminAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
