using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Tests.Support;

public class FakeSpacesClient : ISpacesClient
{
    public Func<Guid, SpaceBookability?> Behavior { get; set; } = _ => DefaultSpace();

    public Task<SpaceBookability?> GetBookabilityAsync(
        Guid spaceId, DateTime from, DateTime to, CancellationToken cancellationToken) =>
        Task.FromResult(Behavior(spaceId));

    public static SpaceBookability DefaultSpace() =>
        new(true, null, "Laboratorio Informatica 2", "Lab", "Edificio B", 24, SpaceAccessMode.Dedicated);
}
