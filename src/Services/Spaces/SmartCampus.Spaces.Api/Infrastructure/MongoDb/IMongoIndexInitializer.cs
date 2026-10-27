namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public interface IMongoIndexInitializer
{
    Task InitializeIndexesAsync(CancellationToken cancellationToken = default);
}
