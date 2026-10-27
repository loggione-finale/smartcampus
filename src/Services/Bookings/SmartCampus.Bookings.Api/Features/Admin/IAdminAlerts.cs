using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public interface IAdminAlerts
{
    Task RaiseAsync(AdminAlertKind kind, Guid bookingId, string message, CancellationToken cancellationToken = default);
}
