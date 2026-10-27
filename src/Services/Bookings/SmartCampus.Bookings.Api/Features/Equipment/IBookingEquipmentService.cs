using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Equipment;

public interface IBookingEquipmentService
{
    Task<EquipmentResolutionPlan?> PreparePlanAsync(
        Guid spaceId,
        IReadOnlyList<NormalizedEquipmentItem> requestedItems,
        CancellationToken cancellationToken = default);

    List<EquipmentRequest> BuildEntities(
        Guid bookingId,
        EquipmentResolutionPlan plan,
        DateTime createdAtUtc);

    Task RaiseMissingEquipmentAlertsAsync(
        Booking booking,
        EquipmentResolutionPlan plan,
        CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> GetByBookingIdAsync(
        Guid bookingId,
        Guid callerUserId,
        string? callerEmail,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> AddToBookingAsync(
        Guid bookingId,
        Guid callerUserId,
        SubmitEquipmentRequestsPayload payload,
        CancellationToken cancellationToken = default);
}
