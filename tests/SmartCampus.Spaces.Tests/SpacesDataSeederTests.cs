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

    [Fact]
    public async Task SeedAsync_LoadsFromBaseDirectory_IndependentOfContentRootPath()
    {
        Assert.True(File.Exists(SpacesDataSeeder.DefaultFilePath));

        _environment.ContentRootPath.Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        _collection.CountDocumentsAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<CountOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0L));

        List<Space>? insertedSpaces = null;
        await _collection.InsertManyAsync(
            Arg.Do<IEnumerable<Space>>(s => insertedSpaces = s.ToList()),
            Arg.Any<InsertManyOptions>(),
            Arg.Any<CancellationToken>());

        var seeder = new SpacesDataSeeder(_dbContext, _environment, _configuration, _logger);

        await seeder.SeedAsync();

        Assert.NotNull(insertedSpaces);
        Assert.Equal(8, insertedSpaces.Count);

        var studio = insertedSpaces.Single(s => s.Id == "3f2a9c1e-0000-4000-8000-000000000003");
        Assert.Equal("Sala Studio Piano Terra", studio.Name);
        Assert.Equal("Edificio A", studio.Building);
        Assert.Equal(SpaceType.StudyRoom, studio.Type);
        Assert.Equal(SpaceMode.Mixed, studio.Mode);
        Assert.Equal(12, studio.Capacity);

        var maintenanceSpace = insertedSpaces.Single(s => s.Id == "3f2a9c1e-0000-4000-8000-000000000008");
        var unavailability = Assert.Single(maintenanceSpace.Unavailabilities);
        Assert.Equal("b7e1c2d3-0000-4000-8000-000000000001", unavailability.CreatedBy);
    }
}
