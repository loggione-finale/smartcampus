using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Infrastructure.Data;

public class SpacesDataSeeder : ISpacesDataSeeder
{
    public static string DefaultFilePath =>
        Path.Combine(AppContext.BaseDirectory, "data", "synthetic", "spaces.json");

    private readonly ISpacesDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpacesDataSeeder> _logger;

    public SpacesDataSeeder(
        ISpacesDbContext dbContext,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<SpacesDataSeeder> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogInformation("Skipping Spaces synthetic seed: environment is '{EnvironmentName}', not Development.", _environment.EnvironmentName);
            return;
        }

        try
        {
            var collection = _dbContext.Spaces;
            var count = await collection.CountDocumentsAsync(FilterDefinition<Space>.Empty, cancellationToken: cancellationToken);

            if (count > 0)
            {
                _logger.LogInformation("Spaces collection already contains {Count} documents. Skipping synthetic seed.", count);
                return;
            }

            var filePath = ResolveSyntheticDataFilePath();
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Synthetic spaces data file '{FilePath}' not found. Skipping seed.", filePath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var spaces = BsonSerializer.Deserialize<List<Space>>(jsonContent);

            if (spaces != null && spaces.Count > 0)
            {
                await collection.InsertManyAsync(spaces, cancellationToken: cancellationToken);
                _logger.LogInformation("Successfully seeded {Count} spaces from '{FilePath}'.", spaces.Count, filePath);
            }
        }
        catch (MongoException ex)
        {
            _logger.LogWarning(ex, "Could not seed Spaces synthetic data: MongoDB unreachable or connection failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while seeding Spaces synthetic data.");
            throw;
        }
    }

    private string ResolveSyntheticDataFilePath()
    {
        var configuredPath = _configuration["SyntheticData:SpacesFilePath"];
        return !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : DefaultFilePath;
    }
}
