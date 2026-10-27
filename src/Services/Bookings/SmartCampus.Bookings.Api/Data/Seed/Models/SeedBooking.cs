using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedBooking(
    string ReferenceCode,
    string Space,
    string Organizer,
    int DaysFromToday,
    string StartTime,
    int DurationMinutes,
    BookingStatus Status,
    int SeatsRequested,
    bool IsGroup,
    string? CancellationReason,
    IReadOnlyList<SeedInvitation>? Invitations,
    IReadOnlyList<SeedEquipmentRequest>? EquipmentRequests,
    SeedClosingComment? ClosingComment,
    IReadOnlyList<SeedAlert>? Alerts);
