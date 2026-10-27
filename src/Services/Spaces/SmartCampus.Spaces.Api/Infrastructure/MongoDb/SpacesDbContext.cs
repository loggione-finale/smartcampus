using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public class SpacesDbContext : ISpacesDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbOptions _options;

    public SpacesDbContext(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        _options = options.Value;
        _database = client.GetDatabase(_options.DatabaseName);
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<Space> Spaces => _database.GetCollection<Space>(_options.SpacesCollectionName);
}
