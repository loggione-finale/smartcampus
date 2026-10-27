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
                // Indice composto per ricerca con filtri (type, capacity, isActive) come da docs/data-model.md
                new(
                    builder.Ascending(s => s.Type).Ascending(s => s.Capacity).Ascending(s => s.IsActive),
                    new CreateIndexOptions { Name = "ix_spaces_type_capacity_isActive" }
                ),
                // Indice per software installato nei laboratori come da docs/data-model.md
                new(
                    builder.Ascending("features.software"),
                    new CreateIndexOptions { Name = "ix_spaces_features_software" }
                ),
                // Indice testuale per ricerca per nome come da docs/data-model.md
                new(
                    builder.Text(s => s.Name),
                    new CreateIndexOptions { Name = "ix_spaces_name_text" }
                ),
                // Indice per modalità (Dedicated / Mixed)
                new(
                    builder.Ascending(s => s.Mode),
                    new CreateIndexOptions { Name = "ix_spaces_mode" }
                ),
                // Indice per strumenti disponibili (equipment)
                new(
                    builder.Ascending(s => s.Equipment),
                    new CreateIndexOptions { Name = "ix_spaces_equipment" }
                )
            };

            await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
            _logger.LogInformation("MongoDB indexes initialized successfully.");
        }
        catch (MongoException ex)
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
