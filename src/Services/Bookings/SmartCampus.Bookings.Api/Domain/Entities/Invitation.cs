using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string Email { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? DisplayNameSnapshot { get; set; }
    public ParticipantRole Role { get; set; }
    public InvitationStatus Status { get; set; }
    public NpgsqlRange<DateTime> Period { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
