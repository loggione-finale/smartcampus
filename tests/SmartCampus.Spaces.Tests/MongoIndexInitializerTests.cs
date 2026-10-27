using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class MongoIndexInitializerTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly IMongoIndexManager<Space> _indexes = Substitute.For<IMongoIndexManager<Space>>();
    private readonly ILogger<MongoIndexInitializer> _logger = Substitute.For<ILogger<MongoIndexInitializer>>();

    public MongoIndexInitializerTests()
    {
        _collection.Indexes.Returns(_indexes);
        _dbContext.Spaces.Returns(_collection);
    }

    [Fact]
    public async Task InitializeIndexesAsync_Creates_Required_Indexes()
    {
        var initializer = new MongoIndexInitializer(_dbContext, _logger);
        IEnumerable<CreateIndexModel<Space>>? capturedModels = null;

        await _indexes.CreateManyAsync(
            Arg.Do<IEnumerable<CreateIndexModel<Space>>>(models => capturedModels = models),
            Arg.Any<CancellationToken>());

        await initializer.InitializeIndexesAsync();

        await _indexes.Received(1).CreateManyAsync(
            Arg.Any<IEnumerable<CreateIndexModel<Space>>>(),
            Arg.Any<CancellationToken>());

        Assert.NotNull(capturedModels);
        var list = capturedModels.ToList();
        Assert.Equal(5, list.Count);

        var names = list.Select(m => m.Options.Name).ToList();
        Assert.Contains("ix_spaces_type_capacity_isActive", names);
        Assert.Contains("ix_spaces_features_software", names);
        Assert.Contains("ix_spaces_name_text", names);
        Assert.Contains("ix_spaces_mode", names);
        Assert.Contains("ix_spaces_equipment", names);
    }

    [Fact]
    public async Task InitializeIndexesAsync_Is_Idempotent_When_Called_Multiple_Times()
    {
        var initializer = new MongoIndexInitializer(_dbContext, _logger);

        // Calling first time
        await initializer.InitializeIndexesAsync();

        // Calling second time (simulating consecutive starts or re-runs)
        var exception = await Record.ExceptionAsync(() => initializer.InitializeIndexesAsync());
        Assert.Null(exception);

        await _indexes.Received(2).CreateManyAsync(
            Arg.Any<IEnumerable<CreateIndexModel<Space>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeIndexesAsync_Handles_MongoException_Gracefully()
    {
        _indexes.CreateManyAsync(
            Arg.Any<IEnumerable<CreateIndexModel<Space>>>(),
            Arg.Any<CancellationToken>())
            .Throws(new MongoConnectionException(new MongoDB.Driver.Core.Connections.ConnectionId(new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(), new System.Net.DnsEndPoint("localhost", 27017))), "Mongo is offline"));

        var initializer = new MongoIndexInitializer(_dbContext, _logger);

        // Should not throw, but log warning and continue
        var exception = await Record.ExceptionAsync(() => initializer.InitializeIndexesAsync());
        Assert.Null(exception);
    }
}
