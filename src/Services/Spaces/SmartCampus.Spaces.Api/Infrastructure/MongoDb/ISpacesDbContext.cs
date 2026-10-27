using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public interface ISpacesDbContext
{
    IMongoDatabase Database { get; }
    IMongoCollection<Space> Spaces { get; }
}
