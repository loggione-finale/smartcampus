using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Admin;

public record DashboardParticipantResponse(string Email, string? DisplayName, ParticipantRole Role, InvitationStatus Status);

public record DashboardBookingResponse(
    string ReferenceCode,
    string Organizer,
    DateTime Start,
    DateTime End,
    BookingStatus Status,
    int OccupiedSeats,
    List<DashboardParticipantResponse> Participants);

public record DashboardSpaceResponse(
    Guid SpaceId,
    string SpaceName,
    string SpaceType,
    string SpaceBuilding,
    int Capacity,
    List<DashboardBookingResponse> Bookings);
