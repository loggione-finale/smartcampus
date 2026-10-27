using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedSpace(
    string Key,
    Guid Id,
    string Name,
    string Type,
    string Building,
    SpaceAccessMode AccessMode,
    int Capacity);
