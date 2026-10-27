using NpgsqlTypes;

namespace SmartCampus.Bookings.Api.Domain;

public class Booking
{
    public Guid Id { get; set; }
    public string ReferenceCode { get; set; } = null!;
    public Guid SpaceId { get; set; }
    public string SpaceNameSnapshot { get; set; } = null!;
    public string SpaceTypeSnapshot { get; set; } = null!;
    public string SpaceBuildingSnapshot { get; set; } = null!;
    public SpaceAccessMode SpaceAccessModeSnapshot { get; set; }
    public int SpaceCapacitySnapshot { get; set; }
    public Guid OrganizerId { get; set; }
    public string OrganizerNameSnapshot { get; set; } = null!;
    public NpgsqlRange<DateTime> Period { get; set; }
    public int SeatsRequested { get; set; }
    public bool IsGroup { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public List<Invitation> Invitations { get; set; } = [];
    public List<EquipmentRequest> EquipmentRequests { get; set; } = [];
    public ClosingComment? ClosingComment { get; set; }
}
