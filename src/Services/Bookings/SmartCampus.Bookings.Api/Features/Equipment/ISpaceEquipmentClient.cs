namespace SmartCampus.Bookings.Api.Features.Equipment;

public interface ISpaceEquipmentClient
{
    Task<IReadOnlySet<string>?> GetSpaceEquipmentCodesAsync(
        Guid spaceId,
        CancellationToken cancellationToken = default);
}
