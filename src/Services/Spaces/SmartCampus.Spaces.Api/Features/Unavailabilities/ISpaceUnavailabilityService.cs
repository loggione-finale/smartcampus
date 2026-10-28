using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Unavailabilities.Models;

namespace SmartCampus.Spaces.Api.Features.Unavailabilities;

public interface ISpaceUnavailabilityService
{
    Task<IReadOnlyList<UnavailabilityResponse>?> ListBySpaceIdAsync(
        string spaceId,
        (DateTime FromUtc, DateTime ToUtc)? periodFilter = null,
        CancellationToken cancellationToken = default);

    Task<UnavailabilityResponse?> CreateAsync(
        string spaceId,
        ValidatedUnavailabilityInput input,
        string createdBy,
        CancellationToken cancellationToken = default);

    Task<DeleteUnavailabilityResult> DeleteAsync(
        string spaceId,
        string unavailabilityId,
        CancellationToken cancellationToken = default);
}
