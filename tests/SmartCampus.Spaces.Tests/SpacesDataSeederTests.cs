using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Infrastructure.Data;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpacesDataSeederTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly ILogger<SpacesDataSeeder> _logger = Substitute.For<ILogger<SpacesDataSeeder>>();

    public SpacesDataSeederTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _environment.EnvironmentName.Returns(Environments.Development);
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseIsEmpty_AndEnvironmentIsDevelopment_SeedsSpaces()
    {
        _collection.CountDocumentsAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<CountOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0L));

        var seeder = new SpacesDataSeeder(_dbContext, _environment, _configuration, _logger);

        await seeder.SeedAsync();

        await _collection.Received(1).InsertManyAsync(
            Arg.Is<IEnumerable<Space>>(spaces => spaces.Any()),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_WhenDatabaseAlreadyHasData_SkipsSeeding()
    {
        _collection.CountDocumentsAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<CountOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(8L));

        var seeder = new SpacesDataSeeder(_dbContext, _environment, _configuration, _logger);

        await seeder.SeedAsync();

        await _collection.DidNotReceive().InsertManyAsync(
            Arg.Any<IEnumerable<Space>>(),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_ConsecutiveRuns_DoNotDuplicateData()
    {
        var count = 0L;
        _collection.CountDocumentsAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<CountOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(count));

        _collection.When(c => c.InsertManyAsync(
            Arg.Any<IEnumerable<Space>>(),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>()))
            .Do(_ => count = 8L);

        var seeder = new SpacesDataSeeder(_dbContext, _environment, _configuration, _logger);

        // First run (db is empty)
        await seeder.SeedAsync();

        // Second run (db now has 8 spaces)
        await seeder.SeedAsync();

        // Only one insert was performed
        await _collection.Received(1).InsertManyAsync(
            Arg.Any<IEnumerable<Space>>(),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_WhenEnvironmentIsNotDevelopment_SkipsSeeding()
    {
        _environment.EnvironmentName.Returns(Environments.Production);

        var seeder = new SpacesDataSeeder(_dbContext, _environment, _configuration, _logger);

        await seeder.SeedAsync();

        await _collection.DidNotReceive().CountDocumentsAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<CountOptions>(),
            Arg.Any<CancellationToken>());

        await _collection.DidNotReceive().InsertManyAsync(
            Arg.Any<IEnumerable<Space>>(),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>());
    }
}
