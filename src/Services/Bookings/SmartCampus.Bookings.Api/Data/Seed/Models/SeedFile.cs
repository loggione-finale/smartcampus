namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedFile(
    IReadOnlyList<SeedUser> Users,
    IReadOnlyList<SeedSpace> Spaces,
    IReadOnlyList<SeedBooking> Bookings);
