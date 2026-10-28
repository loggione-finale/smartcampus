using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Management.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Features.Management;

public sealed class SpaceManagementService(ISpacesDbContext dbContext) : ISpaceManagementService
{
    public async Task<SpaceResponse> CreateAsync(
        ValidatedSpaceInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var now = DateTime.UtcNow;
        var space = new Space
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = input.Name,
            Type = input.Type,
            Mode = input.Mode,
            Building = input.Building,
            Floor = input.Floor,
            Capacity = input.Capacity,
            IsActive = true,
            Equipment = input.Equipment,
            Features = input.Features,
            Unavailabilities = [],
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.Spaces.InsertOneAsync(space, cancellationToken: cancellationToken);
        return SpaceResponse.From(space);
    }

    public async Task<SpaceResponse?> UpdateAsync(
        string id,
        ValidatedSpaceInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var normalizedId = NormalizeId(id);
        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedId);
        var update = Builders<Space>.Update
            .Set(s => s.Name, input.Name)
            .Set(s => s.Type, input.Type)
            .Set(s => s.Mode, input.Mode)
            .Set(s => s.Building, input.Building)
            .Set(s => s.Floor, input.Floor)
            .Set(s => s.Capacity, input.Capacity)
            .Set(s => s.Equipment, input.Equipment)
            .Set(s => s.Features, input.Features)
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

    public async Task<SpaceResponse?> SetActiveStateAsync(
        string id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeId(id);
        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedId);
        var update = Builders<Space>.Update
            .Set(s => s.IsActive, isActive)
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
