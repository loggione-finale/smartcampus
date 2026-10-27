using SmartCampus.Spaces.Api.Features.Catalog.Models;

namespace SmartCampus.Spaces.Api.Features.Catalog;

public interface ISpaceCatalogService
{
    Task<IReadOnlyList<SpaceResponse>> SearchAsync(
        SpaceSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<SpaceResponse?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}
