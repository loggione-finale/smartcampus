using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class EquipmentRequest
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string EquipmentType { get; set; } = null!;
    public string? Notes { get; set; }
    public EquipmentRequestStatus Status { get; set; }
    public Guid? HandledBy { get; set; }
    public DateTime? HandledAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
