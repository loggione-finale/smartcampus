using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Management;
using SmartCampus.Spaces.Api.Features.Management.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceManagementServiceTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly SpaceManagementService _service;

    public SpaceManagementServiceTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _service = new SpaceManagementService(_dbContext);
    }

    [Fact]
    public async Task CreateAsync_InsertsActiveSpaceWithGeneratedUuidAndReturnsResponse()
    {
        Space? inserted = null;
        await _collection.InsertOneAsync(
            Arg.Do<Space>(s => inserted = s),
            Arg.Any<InsertOneOptions>(),
            Arg.Any<CancellationToken>());

        var input = new ValidatedSpaceInput(
            Name: "Aula 301",
            Type: SpaceType.Classroom,
            Mode: SpaceMode.Mixed,
            Building: "Edificio C",
            Floor: 3,
            Capacity: 25,
            Equipment: ["videoprojector", "interactive-whiteboard"],
            Features: new Dictionary<string, object> { ["projector"] = true });

        var response = await _service.CreateAsync(input);

        Assert.NotNull(inserted);
        Assert.True(Guid.TryParse(inserted!.Id, out _));
        Assert.Equal(inserted.Id, response.Id);
        Assert.True(response.IsActive);
        Assert.Equal("Aula 301", response.Name);
        Assert.Equal(SpaceType.Classroom, response.Type);
        Assert.Equal(SpaceMode.Mixed, response.Mode);
        Assert.Equal("Edificio C", response.Building);
        Assert.Equal(3, response.Floor);
        Assert.Equal(25, response.Capacity);
        Assert.Equal(["videoprojector", "interactive-whiteboard"], response.Equipment);
    }

    [Fact]
    public async Task UpdateAsync_NormalizesUppercaseGuidAndReturnsMappedUpdatedSpace()
    {
        FilterDefinition<Space>? capturedFilter = null;
        var updatedEntity = new Space
        {
            Id = "3f2a9c1e-0000-4000-8000-000000000001",
            Name = "Aula 101 Rinnovata",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Mixed,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 40,
            IsActive = true,
            Equipment = ["videoprojector", "audio-kit"],
            Features = new Dictionary<string, object> { ["projector"] = true }
        };

        _collection.FindOneAndUpdateAsync(
            Arg.Do<FilterDefinition<Space>>(f => capturedFilter = f),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(updatedEntity));

        var input = new ValidatedSpaceInput(
            Name: "Aula 101 Rinnovata",
            Type: SpaceType.Classroom,
            Mode: SpaceMode.Mixed,
            Building: "Edificio A",
            Floor: 1,
            Capacity: 40,
            Equipment: ["videoprojector", "audio-kit"],
            Features: new Dictionary<string, object> { ["projector"] = true });

        var response = await _service.UpdateAsync("3F2A9C1E-0000-4000-8000-000000000001", input);

        Assert.NotNull(response);
        Assert.Equal(40, response!.Capacity);
        Assert.Equal(SpaceMode.Mixed, response.Mode);

        Assert.NotNull(capturedFilter);
        var registry = BsonSerializer.SerializerRegistry;
        var rendered = capturedFilter!.Render(new RenderArgs<Space>(registry.GetSerializer<Space>(), registry));
        Assert.Equal(new BsonDocument("_id", "3f2a9c1e-0000-4000-8000-000000000001"), rendered);
    }

    [Fact]
    public async Task UpdateAsync_WhenSpaceDoesNotExist_ReturnsNull()
    {
        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Space>(null!));

        var input = new ValidatedSpaceInput(
            Name: "Aula Fantasma",
            Type: SpaceType.StudyRoom,
            Mode: SpaceMode.Mixed,
            Building: "Edificio Z",
            Floor: 0,
            Capacity: 10,
            Equipment: [],
            Features: []);

        var response = await _service.UpdateAsync(Guid.NewGuid().ToString("D"), input);

        Assert.Null(response);
    }

    [Fact]
    public async Task SetActiveStateAsync_UpdatesIsActiveAndReturnsUpdatedSpace()
    {
        var deactivatedEntity = new Space
        {
            Id = "3f2a9c1e-0000-4000-8000-000000000003",
            Name = "Sala Studio Piano Terra",
            Type = SpaceType.StudyRoom,
            Mode = SpaceMode.Mixed,
            Building = "Edificio A",
            Floor = 0,
            Capacity = 12,
            IsActive = false
        };

        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(deactivatedEntity));

        var response = await _service.SetActiveStateAsync("3f2a9c1e-0000-4000-8000-000000000003", isActive: false);

        Assert.NotNull(response);
        Assert.False(response!.IsActive);
    }

    [Fact]
    public void SpaceUpsertRequest_TryValidateAndNormalize_NormalizesJsonElementsAndDeduplicatesEquipment()
    {
        const string json = """
        {
          "name": "  Laboratorio Robotica  ",
          "type": "Lab",
          "mode": "Dedicated",
          "building": "  Edificio D  ",
          "floor": 2,
          "capacity": 20,
          "equipment": ["videoprojector", "  VIDEOPROJECTOR  ", "audio-kit"],
          "features": {
            "workstations": 20,
            "software": ["ROS 2", "Python"],
            "projector": true
          }
        }
        """;

        var request = JsonSerializer.Deserialize<CreateSpaceRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            })!;

        Assert.True(request.TryValidateAndNormalize(out var validated, out var error));
        Assert.Null(error);
        Assert.NotNull(validated);
        Assert.Equal("Laboratorio Robotica", validated!.Name);
        Assert.Equal("Edificio D", validated.Building);
        Assert.Equal(["videoprojector", "audio-kit"], validated.Equipment);
        Assert.Equal(20, validated.Features["workstations"]);
        Assert.Equal(true, validated.Features["projector"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(501)]
    public void SpaceUpsertRequest_WithOutOfRangeCapacity_FailsValidation(int invalidCapacity)
    {
        var request = new CreateSpaceRequest
        {
            Name = "Aula Test",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = invalidCapacity
        };

        Assert.False(request.TryValidateAndNormalize(out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void SpaceUpsertRequest_WithTenThousandCharNameOrUnsafeFeatureKeys_FailsValidation()
    {
        // Security test from docs/security-and-testing.md: 10,000-char name -> 400
        var hugeNameRequest = new CreateSpaceRequest
        {
            Name = new string('X', 10_000),
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30
        };

        Assert.False(hugeNameRequest.TryValidateAndNormalize(out _, out var hugeError));
        Assert.NotNull(hugeError);

        // NoSQL operator key injection attempt in features -> rejected
        var unsafeFeatureRequest = new CreateSpaceRequest
        {
            Name = "Aula Sicurezza",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30,
            Features = new Dictionary<string, object?> { ["$where"] = "1 == 1" }
        };

        Assert.False(unsafeFeatureRequest.TryValidateAndNormalize(out _, out var unsafeError));
        Assert.NotNull(unsafeError);
    }

    [Fact]
    public async Task SearchAsync_WhenIncludeInactiveIsTrue_OmitsIsActiveFilter()
    {
        FilterDefinition<Space>? capturedFilter = null;
        var cursor = Substitute.For<IAsyncCursor<Space>>();
        cursor.Current.Returns([]);
        cursor.MoveNext(Arg.Any<CancellationToken>()).Returns(false);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        _collection.FindAsync(
            Arg.Do<FilterDefinition<Space>>(f => capturedFilter = f),
            Arg.Any<FindOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var catalogService = new SpaceCatalogService(_dbContext);
        var filter = new SpaceSearchFilter(
            Type: null,
            Mode: null,
            MinCapacity: null,
            Building: null,
            Equipment: [],
            Software: [],
            Search: null,
            IncludeInactive: true);

        await catalogService.SearchAsync(filter);

        Assert.NotNull(capturedFilter);
        var registry = BsonSerializer.SerializerRegistry;
        var rendered = capturedFilter!.Render(new RenderArgs<Space>(registry.GetSerializer<Space>(), registry));
        Assert.Equal(new BsonDocument(), rendered);
    }
}
