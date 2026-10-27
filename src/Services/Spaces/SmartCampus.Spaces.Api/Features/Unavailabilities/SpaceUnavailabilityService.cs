using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Unavailabilities.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Features.Unavailabilities;

public sealed class SpaceUnavailabilityService(ISpacesDbContext dbContext) : ISpaceUnavailabilityService
{
    public async Task<IReadOnlyList<UnavailabilityResponse>?> ListBySpaceIdAsync(
        string spaceId,
        (DateTime FromUtc, DateTime ToUtc)? periodFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return null;
        }

        var normalizedId = NormalizeId(spaceId);
        var space = await dbContext.Spaces
            .Find(Builders<Space>.Filter.Eq(s => s.Id, normalizedId))
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
        {
            return null;
        }

        IEnumerable<Unavailability> items = space.Unavailabilities ?? [];

        if (periodFilter.HasValue)
        {
            var (fromUtc, toUtc) = periodFilter.Value;
            items = items.Where(u => u.Start < toUtc && u.End > fromUtc);
        }

        return items
            .OrderBy(u => u.Start)
            .ThenBy(u => u.End)
            .ThenBy(u => u.Id, StringComparer.Ordinal)
            .Select(UnavailabilityResponse.From)
            .ToList();
    }

    public async Task<UnavailabilityResponse?> CreateAsync(
        string spaceId,
        ValidatedUnavailabilityInput input,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return null;
        }

        var normalizedSpaceId = NormalizeId(spaceId);
        var unavailability = new Unavailability
        {
            Id = Guid.NewGuid().ToString("D"),
            Start = DateTime.SpecifyKind(input.StartUtc, DateTimeKind.Utc),
            End = DateTime.SpecifyKind(input.EndUtc, DateTimeKind.Utc),
            Reason = input.Reason,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "admin" : createdBy.Trim()
        };

        var filter = Builders<Space>.Filter.Eq(s => s.Id, normalizedSpaceId);
        var update = Builders<Space>.Update
            .Push(s => s.Unavailabilities, unavailability)
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

        return updated is null ? null : UnavailabilityResponse.From(unavailability);
    }

    public async Task<DeleteUnavailabilityResult> DeleteAsync(
        string spaceId,
        string unavailabilityId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return DeleteUnavailabilityResult.SpaceNotFound;
        }

        if (string.IsNullOrWhiteSpace(unavailabilityId))
        {
            return DeleteUnavailabilityResult.UnavailabilityNotFound;
        }

        var normalizedSpaceId = NormalizeId(spaceId);
        var normalizedUnavailabilityId = NormalizeId(unavailabilityId);

        var matchFilter = Builders<Space>.Filter.And(
            Builders<Space>.Filter.Eq(s => s.Id, normalizedSpaceId),
            Builders<Space>.Filter.ElemMatch(
                s => s.Unavailabilities,
                u => u.Id == normalizedUnavailabilityId));

        var pullUpdate = Builders<Space>.Update
            .PullFilter(s => s.Unavailabilities, u => u.Id == normalizedUnavailabilityId)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var updated = await dbContext.Spaces.FindOneAndUpdateAsync(
            matchFilter,
            pullUpdate,
            new FindOneAndUpdateOptions<Space, Space> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (updated is not null)
        {
            return DeleteUnavailabilityResult.Deleted;
        }

        var existingSpace = await dbContext.Spaces
            .Find(Builders<Space>.Filter.Eq(s => s.Id, normalizedSpaceId))
            .FirstOrDefaultAsync(cancellationToken);

        return existingSpace is not null
            ? DeleteUnavailabilityResult.UnavailabilityNotFound
            : DeleteUnavailabilityResult.SpaceNotFound;
    }

    private static string NormalizeId(string id) =>
        Guid.TryParse(id, out var parsedGuid) ? parsedGuid.ToString("D") : id.Trim();
}
