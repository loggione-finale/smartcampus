using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Features.Catalog;

public sealed class SpaceCatalogService(ISpacesDbContext dbContext) : ISpaceCatalogService
{
    public async Task<IReadOnlyList<SpaceResponse>> SearchAsync(
        SpaceSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var builder = Builders<Space>.Filter;
        var filters = new List<FilterDefinition<Space>>
        {
            builder.Eq(s => s.IsActive, true)
        };

        if (filter.Type.HasValue)
        {
            filters.Add(builder.Eq(s => s.Type, filter.Type.Value));
        }

        if (filter.Mode.HasValue)
        {
            filters.Add(builder.Eq(s => s.Mode, filter.Mode.Value));
        }

        if (filter.MinCapacity.HasValue)
        {
            filters.Add(builder.Gte(s => s.Capacity, filter.MinCapacity.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Building))
        {
            var escapedBuilding = Regex.Escape(filter.Building);
            filters.Add(builder.Regex(s => s.Building, new BsonRegularExpression($"^{escapedBuilding}$", "i")));
        }

        foreach (var equipmentItem in filter.Equipment)
        {
            var escapedItem = Regex.Escape(equipmentItem);
            filters.Add(builder.Regex("equipment", new BsonRegularExpression($"^{escapedItem}$", "i")));
        }

        foreach (var softwareItem in filter.Software)
        {
            var escapedSoftware = Regex.Escape(softwareItem);
            filters.Add(builder.Regex("features.software", new BsonRegularExpression($"^{escapedSoftware}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escapedSearch = Regex.Escape(filter.Search);
            filters.Add(builder.Regex(s => s.Name, new BsonRegularExpression(escapedSearch, "i")));
        }

        var combinedFilter = filters.Count == 1 ? filters[0] : builder.And(filters);
        var sort = Builders<Space>.Sort
            .Ascending(s => s.Building)
            .Ascending(s => s.Floor)
            .Ascending(s => s.Name);

        var skip = (filter.Page - 1) * filter.PageSize;

        var spaces = await dbContext.Spaces
            .Find(combinedFilter)
            .Sort(sort)
            .Skip(skip)
            .Limit(filter.PageSize)
            .ToListAsync(cancellationToken);

        return spaces.Select(SpaceResponse.From).ToList();
    }

    public async Task<SpaceResponse?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = Guid.TryParse(id, out var parsedGuid)
            ? parsedGuid.ToString("D")
            : id.Trim();

        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedId);
        var space = await dbContext.Spaces
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return space is null ? null : SpaceResponse.From(space);
    }
}
