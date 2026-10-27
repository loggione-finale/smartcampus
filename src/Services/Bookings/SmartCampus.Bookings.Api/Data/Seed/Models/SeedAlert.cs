using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedAlert(AdminAlertKind Kind, string Message, AdminAlertStatus Status);
