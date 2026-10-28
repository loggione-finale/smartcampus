using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

/// <summary>
/// Gestisce la creazione degli avvisi per gli amministratori del campus.
/// Nota sull'ambito transazionale: AdminAlertsService è registrato come Scoped e condivide il BookingsDbContext
/// con gli altri servizi della richiesta HTTP corrente. Salva immediatamente l'avviso persistendo anche
/// le eventuali entità già tracciate nel medesimo contesto.
/// </summary>
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
