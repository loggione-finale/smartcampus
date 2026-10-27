using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Management.Models;

namespace SmartCampus.Spaces.Api.Features.Management;

public interface ISpaceManagementService
{
    Task<SpaceResponse> CreateAsync(
        ValidatedSpaceInput input,
        CancellationToken cancellationToken = default);

    Task<SpaceResponse?> UpdateAsync(
        string id,
        ValidatedSpaceInput input,
        CancellationToken cancellationToken = default);

    Task<SpaceResponse?> SetActiveStateAsync(
        string id,
        bool isActive,
        CancellationToken cancellationToken = default);
}
