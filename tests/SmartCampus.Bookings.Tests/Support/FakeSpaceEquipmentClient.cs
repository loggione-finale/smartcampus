using SmartCampus.Bookings.Api.Features.Equipment;

namespace SmartCampus.Bookings.Tests.Support;

public class FakeSpaceEquipmentClient : ISpaceEquipmentClient
{
    public Func<Guid, IReadOnlySet<string>?> Behavior { get; set; } = _ => DefaultEquipment();

    public Task<IReadOnlySet<string>?> GetSpaceEquipmentCodesAsync(
        Guid spaceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Behavior(spaceId));

    public static IReadOnlySet<string> DefaultEquipment() =>
        new HashSet<string>(["videoprojector"], StringComparer.OrdinalIgnoreCase);
}
