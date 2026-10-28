using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceCatalogServiceTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly SpaceCatalogService _service;

    public SpaceCatalogServiceTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _service = new SpaceCatalogService(_dbContext);
    }

    private void SetupSpacesCursor(
        IReadOnlyList<Space> spaces,
        Action<FilterDefinition<Space>, FindOptions<Space, Space>>? onQuery = null)
    {
        var cursor = Substitute.For<IAsyncCursor<Space>>();
        cursor.Current.Returns(spaces);
        cursor.MoveNext(Arg.Any<CancellationToken>()).Returns(spaces.Count > 0, false);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(spaces.Count > 0), Task.FromResult(false));

        _collection.FindAsync(
            Arg.Do<FilterDefinition<Space>>(f => { _lastFilter = f; }),
            Arg.Do<FindOptions<Space, Space>>(o =>
            {
                _lastOptions = o;
                if (_lastFilter is not null)
                {
                    onQuery?.Invoke(_lastFilter, o);
                }
            }),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }

    private FilterDefinition<Space>? _lastFilter;
    private FindOptions<Space, Space>? _lastOptions;

    private static BsonDocument RenderFilter(FilterDefinition<Space> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var serializer = registry.GetSerializer<Space>();
        return filter.Render(new RenderArgs<Space>(serializer, registry));
    }

    [Fact]
    public async Task SearchAsync_WithDefaultFilter_QueriesOnlyActiveSpacesWithDefaultPaginationAndSort()
    {
        SetupSpacesCursor(
        [
            new Space
            {
                Id = "3f2a9c1e-0000-4000-8000-000000000001",
                Name = "Aula 101",
                Type = SpaceType.Classroom,
                Mode = SpaceMode.Dedicated,
                Building = "Edificio A",
                Floor = 1,
                Capacity = 30,
                IsActive = true
            }
        ]);

        var filter = new SpaceSearchFilter(
            Type: null,
            Mode: null,
            MinCapacity: null,
            Building: null,
            Equipment: [],
            Software: [],
            Search: null);

        var results = await _service.SearchAsync(filter);

        Assert.Single(results);
        Assert.Equal("Aula 101", results[0].Name);

        Assert.NotNull(_lastFilter);
        var rendered = RenderFilter(_lastFilter!);
        Assert.Equal(new BsonDocument("isActive", true), rendered);

        Assert.NotNull(_lastOptions);
        Assert.Equal(0, _lastOptions!.Skip);
        Assert.Equal(SpaceSearchQuery.DefaultPageSize, _lastOptions.Limit);
        Assert.NotNull(_lastOptions.Sort);
    }

    [Fact]
    public async Task SearchAsync_WithCombinedFilters_BuildsTypedFilterForAllCriteria()
    {
        SetupSpacesCursor([]);

        var filter = new SpaceSearchFilter(
            Type: SpaceType.Lab,
            Mode: SpaceMode.Dedicated,
            MinCapacity: 20,
            Building: "Edificio B",
            Equipment: ["videoprojector", "audio-kit"],
            Software: ["Docker", ".NET SDK"],
            Search: "Informatica",
            Page: 2,
            PageSize: 10);

        var results = await _service.SearchAsync(filter);

        Assert.Empty(results);
        Assert.NotNull(_lastFilter);
        var rendered = RenderFilter(_lastFilter!);
        var clauses = rendered["$and"].AsBsonArray.Select(c => c.AsBsonDocument).ToList();

        Assert.Contains(clauses, c => c.Contains("isActive") && c["isActive"].AsBoolean);
        Assert.Contains(clauses, c => c.Contains("type") && c["type"].AsString == "Lab");
        Assert.Contains(clauses, c => c.Contains("mode") && c["mode"].AsString == "Dedicated");
        Assert.Contains(clauses, c => c.Contains("capacity") && c["capacity"].AsBsonDocument["$gte"].AsInt32 == 20);
        Assert.Contains(clauses, c => c.Contains("building")
            && c["building"].AsBsonRegularExpression.Pattern == @"^Edificio\ B$"
            && c["building"].AsBsonRegularExpression.Options == "i");
        Assert.Contains(clauses, c => c.Contains("equipment")
            && c["equipment"].AsBsonRegularExpression.Pattern == "^videoprojector$"
            && c["equipment"].AsBsonRegularExpression.Options == "i");
        Assert.Contains(clauses, c => c.Contains("equipment")
            && c["equipment"].AsBsonRegularExpression.Pattern == "^audio-kit$"
            && c["equipment"].AsBsonRegularExpression.Options == "i");
        Assert.Contains(clauses, c => c.Contains("features.software")
            && c["features.software"].AsBsonRegularExpression.Pattern == "^Docker$"
            && c["features.software"].AsBsonRegularExpression.Options == "i");
        Assert.Contains(clauses, c => c.Contains("features.software")
            && c["features.software"].AsBsonRegularExpression.Pattern == @"^\.NET\ SDK$"
            && c["features.software"].AsBsonRegularExpression.Options == "i");
        Assert.Contains(clauses, c => c.Contains("name")
            && c["name"].AsBsonRegularExpression.Pattern == "Informatica"
            && c["name"].AsBsonRegularExpression.Options == "i");

        Assert.NotNull(_lastOptions);
        Assert.Equal(10, _lastOptions!.Skip);
        Assert.Equal(10, _lastOptions.Limit);
    }

    [Fact]
    public async Task GetByIdAsync_WithUppercaseGuidAndBsonDeserializedSpace_NormalizesIdAndSerializesFeaturesCleanly()
    {
        var rawBson = BsonDocument.Parse("""
        {
          "_id": "3f2a9c1e-0000-4000-8000-000000000002",
          "name": "Laboratorio Informatica 2",
          "type": "Lab",
          "mode": "Dedicated",
          "building": "Edificio B",
          "floor": 1,
          "capacity": 24,
          "isActive": true,
          "features": {
            "workstations": 24,
            "software": ["Visual Studio Code", "Docker"],
            "projector": true
          },
          "equipment": ["videoprojector"],
          "unavailabilities": [
            {
              "id": "u-2",
              "start": { "$date": "2026-10-29T08:00:00Z" },
              "end": { "$date": "2026-10-29T10:00:00Z" },
              "reason": "Secondo intervento",
              "createdBy": "b7e1c2d3-0000-4000-8000-000000000001"
            },
            {
              "id": "u-1",
              "start": { "$date": "2026-10-28T08:00:00Z" },
              "end": { "$date": "2026-10-28T12:00:00Z" },
              "reason": "Primo intervento",
              "createdBy": "b7e1c2d3-0000-4000-8000-000000000001"
            }
          ],
          "createdAt": { "$date": "2026-10-26T08:00:00Z" },
          "updatedAt": { "$date": "2026-10-26T14:00:00Z" }
        }
        """);

        var deserializedSpace = BsonSerializer.Deserialize<Space>(rawBson);
        SetupSpacesCursor([deserializedSpace]);

        var response = await _service.GetByIdAsync("3F2A9C1E-0000-4000-8000-000000000002");

        Assert.NotNull(response);
        Assert.NotNull(_lastFilter);
        Assert.Equal(
            new BsonDocument("_id", "3f2a9c1e-0000-4000-8000-000000000002"),
            RenderFilter(_lastFilter!));

        // Verify unavailabilities are sorted chronologically and omit CreatedBy
        Assert.Equal(2, response!.Unavailabilities.Count);
        Assert.Equal("u-1", response.Unavailabilities[0].Id);
        Assert.Equal("u-2", response.Unavailabilities[1].Id);

        // Verify System.Text.Json serializes string enums and BSON-backed features cleanly
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Lab", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Dedicated", doc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(24, doc.RootElement.GetProperty("features").GetProperty("workstations").GetInt32());
        Assert.True(doc.RootElement.GetProperty("features").GetProperty("projector").GetBoolean());

        var softwareArray = doc.RootElement.GetProperty("features").GetProperty("software");
        Assert.Equal(JsonValueKind.Array, softwareArray.ValueKind);
        Assert.Equal("Visual Studio Code", softwareArray[0].GetString());
        Assert.Equal("Docker", softwareArray[1].GetString());
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSpaceIsDeactivated_StillReturnsSpaceDetails()
    {
        SetupSpacesCursor(
        [
            new Space
            {
                Id = "3f2a9c1e-0000-4000-8000-000000000099",
                Name = "Aula Storica Disattivata",
                Type = SpaceType.Classroom,
                Mode = SpaceMode.Dedicated,
                Building = "Edificio C",
                Floor = 0,
                Capacity = 40,
                IsActive = false
            }
        ]);

        var response = await _service.GetByIdAsync("3f2a9c1e-0000-4000-8000-000000000099");

        Assert.NotNull(response);
        Assert.False(response!.IsActive);
        Assert.Equal("Aula Storica Disattivata", response.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSpaceNotFound_ReturnsNull()
    {
        SetupSpacesCursor([]);

        var response = await _service.GetByIdAsync("3f2a9c1e-0000-4000-8000-999999999999");

        Assert.Null(response);
    }

    [Fact]
    public void SpaceSearchQuery_TryNormalize_SplitsAndDeduplicatesEquipmentAndRejectsHugeStrings()
    {
        var validQuery = new SpaceSearchQuery
        {
            Type = "lab",
            Mode = "dedicated",
            MinCapacity = 24,
            Equipment = ["videoprojector, audio-kit", "VIDEOPROJECTOR", "microphone"]
        };

        Assert.True(validQuery.TryNormalize(out var filter, out var error));
        Assert.Null(error);
        Assert.NotNull(filter);
        Assert.Equal(SpaceType.Lab, filter!.Type);
        Assert.Equal(SpaceMode.Dedicated, filter.Mode);
        Assert.Equal(["videoprojector", "audio-kit", "microphone"], filter.Equipment);

        // Security requirement from docs/security-and-testing.md: 10,000-character string rejected with 400
        var hugeQuery = new SpaceSearchQuery
        {
            Search = new string('A', 10_000)
        };

        Assert.False(hugeQuery.TryNormalize(out _, out var hugeError));
        Assert.NotNull(hugeError);
    }
}
