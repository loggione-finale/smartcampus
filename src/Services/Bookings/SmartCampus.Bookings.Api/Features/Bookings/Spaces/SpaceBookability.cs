using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Bookings.Spaces;

public record SpaceBookability(
    bool Bookable,
    string? Reason,
    string Name,
    string Type,
    string Building,
    int Capacity,
    SpaceAccessMode AccessMode);
