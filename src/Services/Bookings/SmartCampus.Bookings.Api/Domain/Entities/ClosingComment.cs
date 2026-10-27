using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class ClosingComment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid AuthorId { get; set; }
    public bool SpaceUsed { get; set; }
    public int AttendeesCount { get; set; }
    public bool IssuesReported { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
