using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Equipment.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Features.Equipment;

public sealed class SpaceEquipmentService(ISpacesDbContext dbContext) : ISpaceEquipmentService
{
    public IReadOnlyList<EquipmentCatalogItemResponse> GetCatalog() => EquipmentCatalog.All;

    public async Task<IReadOnlyList<EquipmentCatalogItemResponse>?> GetBySpaceIdAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeId(spaceId);
        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedId);

        var space = await dbContext.Spaces
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return space is null
            ? null
            : EquipmentCatalog.ResolveItems(space.Equipment);
    }

    public async Task<SpaceResponse?> UpdateSpaceEquipmentAsync(
        string spaceId,
        IReadOnlyList<string> equipment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(equipment);

        var normalizedId = NormalizeId(spaceId);
        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedId);
        var update = Builders<Space>.Update
            .Set(s => s.Equipment, equipment.ToList())
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<Space, Space>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await dbContext.Spaces.FindOneAndUpdateAsync(
            filter,
            update,
            options,
            cancellationToken);

        return updated is null ? null : SpaceResponse.From(updated);
    }

    private static string NormalizeId(string id) =>
        Guid.TryParse(id, out var parsedGuid) ? parsedGuid.ToString("D") : id.Trim();
}
