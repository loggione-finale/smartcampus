namespace SmartCampus.Bookings.Api.Features.Bookings;

public record CreateBookingRequest(Guid SpaceId, DateTimeOffset Start, DateTimeOffset End);
