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
