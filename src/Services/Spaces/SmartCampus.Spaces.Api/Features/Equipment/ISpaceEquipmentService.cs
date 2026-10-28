using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Equipment.Models;

namespace SmartCampus.Spaces.Api.Features.Equipment;

public interface ISpaceEquipmentService
{
    IReadOnlyList<EquipmentCatalogItemResponse> GetCatalog();

    Task<IReadOnlyList<EquipmentCatalogItemResponse>?> GetBySpaceIdAsync(
        string spaceId,
        CancellationToken cancellationToken = default);

    Task<SpaceResponse?> UpdateSpaceEquipmentAsync(
        string spaceId,
        IReadOnlyList<string> equipment,
        CancellationToken cancellationToken = default);
}
