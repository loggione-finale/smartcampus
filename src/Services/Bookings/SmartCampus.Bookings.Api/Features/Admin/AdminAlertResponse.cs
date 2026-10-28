using System.ComponentModel.DataAnnotations;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public record ResolveAdminAlertRequest([StringLength(500)] string? Note);

public record AdminAlertResponse(
    Guid Id,
    AdminAlertKind Kind,
    Guid? BookingId,
    string? BookingReferenceCode,
    string Message,
    AdminAlertStatus Status,
    DateTime CreatedAt,
    DateTime? HandledAt,
    string? HandlingNote)
{
    public static AdminAlertResponse From(AdminAlert alert) => new(
        alert.Id,
        alert.Kind,
        alert.BookingId,
        alert.Booking?.ReferenceCode,
        alert.Message,
        alert.Status,
        alert.CreatedAt,
        alert.HandledAt,
        alert.HandlingNote);
}
