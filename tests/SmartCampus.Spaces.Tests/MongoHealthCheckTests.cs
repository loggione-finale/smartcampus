using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Spaces.Api.Infrastructure.Health;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class MongoHealthCheckTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoDatabase _database = Substitute.For<IMongoDatabase>();
    private readonly ILogger<MongoHealthCheck> _logger = Substitute.For<ILogger<MongoHealthCheck>>();

    public MongoHealthCheckTests()
    {
        _dbContext.Database.Returns(_database);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns_Healthy_When_Database_Responds_To_Ping()
    {
        _database.RunCommandAsync<BsonDocument>(
            Arg.Any<Command<BsonDocument>>(),
            Arg.Any<ReadPreference>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BsonDocument("ok", 1)));

        var healthCheck = new MongoHealthCheck(_dbContext, _logger);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Database_Throws_Exception()
    {
        _database.RunCommandAsync<BsonDocument>(
            Arg.Any<Command<BsonDocument>>(),
            Arg.Any<ReadPreference>(),
            Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("Database connection timed out"));

        var healthCheck = new MongoHealthCheck(_dbContext, _logger);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
