namespace SmartCampus.Bookings.Api.Domain;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Closed
}

public enum SpaceAccessMode
{
    Dedicated,
    Mixed
}

public enum ParticipantRole
{
    Organizer,
    Participant
}

public enum InvitationStatus
{
    Invited,
    Accepted,
    Expired,
    Left
}

public enum EquipmentRequestStatus
{
    Pending,
    Confirmed,
    Rejected
}

public enum AdminAlertKind
{
    NewBooking,
    EquipmentRequest,
    Maintenance,
    ReportedIssue
}

public enum AdminAlertStatus
{
    Open,
    Handled
}

public enum OutboxEmailStatus
{
    Pending,
    Sent,
    Failed
}
