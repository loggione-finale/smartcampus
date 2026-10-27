using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed.Models;

public record SeedInvitation(string? User, string? Email, InvitationStatus Status, ParticipantRole Role);
