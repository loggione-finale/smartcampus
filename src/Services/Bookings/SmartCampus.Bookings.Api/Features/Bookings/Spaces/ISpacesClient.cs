namespace SmartCampus.Bookings.Api.Features.Bookings.Spaces;

public interface ISpacesClient
{
    Task<SpaceBookability?> GetBookabilityAsync(Guid spaceId, DateTime from, DateTime to, CancellationToken cancellationToken);
}
