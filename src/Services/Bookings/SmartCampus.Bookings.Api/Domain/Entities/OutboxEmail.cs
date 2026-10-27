using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class OutboxEmail
{
    public Guid Id { get; set; }
    public string ToAddress { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public OutboxEmailStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public Booking? Booking { get; set; }
}
