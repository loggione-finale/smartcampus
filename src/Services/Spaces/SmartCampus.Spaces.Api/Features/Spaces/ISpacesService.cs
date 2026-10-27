using SmartCampus.Spaces.Api.Features.Spaces.Models;

namespace SmartCampus.Spaces.Api.Features.Spaces;

public interface ISpacesService
{
    Task<SpaceBookabilityResponse?> GetBookabilityAsync(
        string id,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
