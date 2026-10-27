using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Spaces.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Features.Spaces;

public class SpacesService(ISpacesDbContext dbContext) : ISpacesService
{
    public async Task<SpaceBookabilityResponse?> GetBookabilityAsync(
        string id,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = id.Trim();

        var space = await dbContext.Spaces
            .Find(s => s.Id == normalizedId)
            .FirstOrDefaultAsync(cancellationToken);

        if (space is null)
        {
            return null;
        }

        if (!space.IsActive)
        {
            return new SpaceBookabilityResponse(
                Bookable: false,
                Reason: "Lo spazio è disattivato",
                Name: space.Name,
                Type: space.Type.ToString(),
                Building: space.Building,
                Capacity: space.Capacity,
                AccessMode: space.Mode);
        }

        var conflict = space.Unavailabilities?
            .FirstOrDefault(u => u.Start < toUtc && u.End > fromUtc);

        if (conflict is not null)
        {
            var reason = !string.IsNullOrWhiteSpace(conflict.Reason)
                ? conflict.Reason
                : "Spazio non disponibile nel periodo selezionato";

            return new SpaceBookabilityResponse(
                Bookable: false,
                Reason: reason,
                Name: space.Name,
                Type: space.Type.ToString(),
                Building: space.Building,
                Capacity: space.Capacity,
                AccessMode: space.Mode);
        }

        return new SpaceBookabilityResponse(
            Bookable: true,
            Reason: null,
            Name: space.Name,
            Type: space.Type.ToString(),
            Building: space.Building,
            Capacity: space.Capacity,
            AccessMode: space.Mode);
    }
}
