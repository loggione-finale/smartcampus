using SmartCampus.Bookings.Api.Features.Equipment;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public record CreateBookingRequest(
    Guid SpaceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<string>? ParticipantEmails = null,
    IReadOnlyList<BookingEquipmentItemInput>? EquipmentRequests = null);
