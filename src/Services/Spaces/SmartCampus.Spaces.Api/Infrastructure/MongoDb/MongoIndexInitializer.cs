using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public class MongoIndexInitializer : IMongoIndexInitializer
{
    private readonly ISpacesDbContext _dbContext;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(ISpacesDbContext dbContext, ILogger<MongoIndexInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating MongoDB indexes for 'spaces' collection if they do not exist...");

            var collection = _dbContext.Spaces;
            var builder = Builders<Space>.IndexKeys;

            var indexModels = new List<CreateIndexModel<Space>>
            {
                // Compound index for search with filters (type, capacity, isActive) per docs/data-model.md
                new(
                    builder.Ascending(s => s.Type).Ascending(s => s.Capacity).Ascending(s => s.IsActive),
                    new CreateIndexOptions { Name = "ix_spaces_type_capacity_isActive" }
                ),
                // Index for software installed in labs per docs/data-model.md
                new(
                    builder.Ascending("features.software"),
                    new CreateIndexOptions { Name = "ix_spaces_features_software" }
                ),
                // Text index for name search per docs/data-model.md
                new(
                    builder.Text(s => s.Name),
                    new CreateIndexOptions { Name = "ix_spaces_name_text" }
                ),
                // Index for mode (Dedicated / Mixed)
                new(
                    builder.Ascending(s => s.Mode),
                    new CreateIndexOptions { Name = "ix_spaces_mode" }
                ),
                // Index for available equipment
                new(
                    builder.Ascending(s => s.Equipment),
                    new CreateIndexOptions { Name = "ix_spaces_equipment" }
                )
            };

            await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
            _logger.LogInformation("MongoDB indexes initialized successfully.");
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            _logger.LogWarning(ex, "Could not initialize MongoDB indexes. Database might be unreachable or starting up.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing MongoDB indexes.");
            throw;
        }
    }
}
