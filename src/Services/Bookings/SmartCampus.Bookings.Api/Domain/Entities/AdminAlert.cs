using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class AdminAlert
{
    public Guid Id { get; set; }
    public AdminAlertKind Kind { get; set; }
    public Guid? BookingId { get; set; }
    public string Message { get; set; } = null!;
    public AdminAlertStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? HandledBy { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? HandlingNote { get; set; }

    public Booking? Booking { get; set; }
}
