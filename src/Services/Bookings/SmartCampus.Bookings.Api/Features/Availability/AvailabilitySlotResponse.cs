namespace SmartCampus.Bookings.Api.Features.Availability;

public record AvailabilitySlotResponse(
    DateTimeOffset Start,
    DateTimeOffset End,
    int OccupiedSeats,
    int FreeSeats,
    bool IsAvailable
);
