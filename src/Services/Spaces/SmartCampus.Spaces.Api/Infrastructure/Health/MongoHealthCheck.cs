using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Infrastructure.Health;

public class MongoHealthCheck : IHealthCheck
{
    private readonly ISpacesDbContext _dbContext;
    private readonly ILogger<MongoHealthCheck> _logger;

    public MongoHealthCheck(ISpacesDbContext dbContext, ILogger<MongoHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pingCommand = new BsonDocument("ping", 1);
            await _dbContext.Database.RunCommandAsync<BsonDocument>(pingCommand, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB connection is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB health check failed: database unreachable.");
            return HealthCheckResult.Unhealthy("MongoDB database is unreachable", ex);
        }
    }
}
