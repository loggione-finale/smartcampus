using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Availability;

public record SpaceAvailabilityResponse(
    Guid SpaceId,
    DateOnly Date,
    SpaceAccessMode AccessMode,
    int Capacity,
    IReadOnlyList<AvailabilitySlotResponse> Slots
);
