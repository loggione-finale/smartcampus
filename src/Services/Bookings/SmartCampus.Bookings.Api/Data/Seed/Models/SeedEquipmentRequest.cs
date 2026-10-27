using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedEquipmentRequest(string EquipmentType, string? Notes, EquipmentRequestStatus Status);
