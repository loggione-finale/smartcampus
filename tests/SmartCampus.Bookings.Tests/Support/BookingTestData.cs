using NpgsqlTypes;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Tests.Support;

public static class BookingTestData
{
    public static Booking NewBooking(
        Guid spaceId,
        DateTime start,
        DateTime end,
        SpaceAccessMode accessMode = SpaceAccessMode.Dedicated,
        BookingStatus status = BookingStatus.Confirmed,
        Guid? organizerId = null)
    {
        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        return new Booking
        {
            Id = Guid.NewGuid(),
            ReferenceCode = $"SC-000000-{Guid.NewGuid():N}"[..20],
            SpaceId = spaceId,
            SpaceNameSnapshot = "Aula 1",
            SpaceTypeSnapshot = "Classroom",
            SpaceBuildingSnapshot = "Edificio A",
            SpaceAccessModeSnapshot = accessMode,
            SpaceCapacitySnapshot = 30,
            OrganizerId = organizerId ?? Guid.NewGuid(),
            OrganizerNameSnapshot = "organizer@smartcampus.test",
            Period = period,
            BookingDate = DateOnly.FromDateTime(start),
            TimeBand = TimeBand.Morning,
            SeatsRequested = 1,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Invitation NewInvitation(
        Booking booking, Guid? userId, InvitationStatus status, ParticipantRole role = ParticipantRole.Participant) =>
        new()
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            Email = $"{Guid.NewGuid():N}@smartcampus.test",
            UserId = userId,
            Role = role,
            Status = status,
            Period = booking.Period,
            ExpiresAt = booking.Period.LowerBound,
            CreatedAt = DateTime.UtcNow
        };
}
