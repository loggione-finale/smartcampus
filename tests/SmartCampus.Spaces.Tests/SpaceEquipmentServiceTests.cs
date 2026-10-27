using System.Text.Json;
using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Catalog.Models;
using SmartCampus.Spaces.Api.Features.Equipment;
using SmartCampus.Spaces.Api.Features.Equipment.Models;
using SmartCampus.Spaces.Api.Features.Management.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceEquipmentServiceTests
{
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly SpaceEquipmentService _service;

    public SpaceEquipmentServiceTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _service = new SpaceEquipmentService(_dbContext);
    }

    [Fact]
    public void GetCatalog_ContainsAllRequiredEquipmentTypes()
    {
        var catalog = _service.GetCatalog();

        Assert.Contains(catalog, item => item.Code == "videoprojector" && item.Name == "Videoproiettore" && item.IsStandard && !item.RequiresNotes);
        Assert.Contains(catalog, item => item.Code == "interactive-whiteboard" && item.Name == "Lavagna interattiva" && item.IsStandard && !item.RequiresNotes);
        Assert.Contains(catalog, item => item.Code == "audio-kit" && item.Name == "Kit audio" && item.IsStandard && !item.RequiresNotes);
        Assert.Contains(catalog, item => item.Code == "microphone" && item.Name == "Microfono" && item.IsStandard && !item.RequiresNotes);
        Assert.Contains(catalog, item => item.Code == "other" && item.Name == "Altro" && !item.IsStandard && item.RequiresNotes);
    }

    [Fact]
    public void UpdateSpaceEquipmentRequest_NormalizesItalianLabelsAliasesAndDeduplicates()
    {
        var request = new UpdateSpaceEquipmentRequest
        {
            Equipment =
            [
                "  Videoproiettore  ",
                "VIDEOPROJECTOR",
                "Lavagna interattiva",
                "Kit audio",
                "Altro"
            ]
        };

        Assert.True(request.TryValidateAndNormalize(out var normalized, out var error));
        Assert.Null(error);
        Assert.Equal(
            ["videoprojector", "interactive-whiteboard", "audio-kit", "other"],
            normalized);
    }

    [Fact]
    public void UpdateSpaceEquipmentRequest_AllowsEmptyArrayToClearRoomEquipment()
    {
        var request = new UpdateSpaceEquipmentRequest
        {
            Equipment = []
        };

        Assert.True(request.TryValidateAndNormalize(out var normalized, out var error));
        Assert.Null(error);
        Assert.Empty(normalized);
    }

    [Fact]
    public void UpdateSpaceEquipmentRequest_RejectsNullUnknownEmptyHugeOrExcessiveItems()
    {
        var nullRequest = new UpdateSpaceEquipmentRequest { Equipment = null };
        Assert.False(nullRequest.TryValidateAndNormalize(out _, out var nullError));
        Assert.Contains("required", nullError, StringComparison.OrdinalIgnoreCase);

        var unknownRequest = new UpdateSpaceEquipmentRequest
        {
            Equipment = ["videoprojector", "macchina-del-caffe"]
        };
        Assert.False(unknownRequest.TryValidateAndNormalize(out _, out var unknownError));
        Assert.Contains("macchina-del-caffe", unknownError, StringComparison.OrdinalIgnoreCase);

        var blankItemRequest = new UpdateSpaceEquipmentRequest
        {
            Equipment = ["   "]
        };
        Assert.False(blankItemRequest.TryValidateAndNormalize(out _, out _));

        var hugeItemRequest = new UpdateSpaceEquipmentRequest
        {
            Equipment = [new string('X', 10_000)]
        };
        Assert.False(hugeItemRequest.TryValidateAndNormalize(out _, out _));

        var tooManyItemsRequest = new UpdateSpaceEquipmentRequest
        {
            Equipment = Enumerable.Range(0, EquipmentCatalog.MaxEquipmentCount + 1)
                .Select(_ => "videoprojector")
                .ToList()
        };
        Assert.False(tooManyItemsRequest.TryValidateAndNormalize(out _, out _));
    }

    [Fact]
    public void SpaceUpsertRequest_And_SpaceSearchQuery_UseOfficialEquipmentCatalog()
    {
        var invalidUpsert = new CreateSpaceRequest
        {
            Name = "Aula Test",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 25,
            Equipment = ["strumento-inesistente"]
        };

        Assert.False(invalidUpsert.TryValidateAndNormalize(out _, out var upsertError));
        Assert.Contains("strumento-inesistente", upsertError, StringComparison.OrdinalIgnoreCase);

        var italianUpsert = new CreateSpaceRequest
        {
            Name = "Aula Test",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 25,
            Equipment = ["Videoproiettore", "Lavagna interattiva"]
        };

        Assert.True(italianUpsert.TryValidateAndNormalize(out var validated, out _));
        Assert.Equal(["videoprojector", "interactive-whiteboard"], validated!.Equipment);

        var searchQuery = new SpaceSearchQuery
        {
            Equipment = ["Videoproiettore, Kit audio"]
        };
        Assert.True(searchQuery.TryNormalize(out var filter, out _));
        Assert.Equal(["videoprojector", "audio-kit"], filter!.Equipment);
    }

    [Fact]
    public async Task GetBySpaceIdAsync_ReturnsNullWhenMissing_AndResolvedCatalogItemsWhenFound()
    {
        var missingCursor = Substitute.For<IAsyncCursor<Space>>();
        missingCursor.Current.Returns([]);
        missingCursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        _collection.FindAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<FindOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(missingCursor));

        var missing = await _service.GetBySpaceIdAsync("3f2a9c1e-0000-4000-8000-000000000099");
        Assert.Null(missing);

        var space = new Space
        {
            Id = "3f2a9c1e-0000-4000-8000-000000000001",
            Name = "Aula 101",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30,
            Equipment = ["videoprojector", "audio-kit"]
        };

        var foundCursor = Substitute.For<IAsyncCursor<Space>>();
        foundCursor.Current.Returns([space]);
        foundCursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));

        _collection.FindAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<FindOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(foundCursor));

        var items = await _service.GetBySpaceIdAsync(space.Id);
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal("Videoproiettore", items[0].Name);
        Assert.Equal("Kit audio", items[1].Name);
    }

    [Fact]
    public async Task UpdateSpaceEquipmentAsync_UpdatesDocumentAndReturnsSpaceResponseWithDetails()
    {
        var spaceId = "3f2a9c1e-0000-4000-8000-000000000001";
        var updatedSpace = new Space
        {
            Id = spaceId,
            Name = "Aula 101",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Floor = 1,
            Capacity = 30,
            IsActive = true,
            Equipment = ["videoprojector", "interactive-whiteboard"],
            UpdatedAt = DateTime.UtcNow
        };

        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Is<FindOneAndUpdateOptions<Space, Space>>(o => o.ReturnDocument == ReturnDocument.After),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Space>(updatedSpace));

        var response = await _service.UpdateSpaceEquipmentAsync(
            spaceId,
            ["videoprojector", "interactive-whiteboard"]);

        Assert.NotNull(response);
        Assert.Equal(["videoprojector", "interactive-whiteboard"], response!.Equipment);
        Assert.Equal(2, response.EquipmentDetails.Count);
        Assert.Equal("Lavagna interattiva", response.EquipmentDetails[1].Name);
    }

    [Fact]
    public void SyntheticSpacesSeed_OnlyContainsRecognizedCatalogEquipmentCodes()
    {
        var seedPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "data", "synthetic", "spaces.json");

        Assert.True(File.Exists(seedPath), $"Expected synthetic seed file at {seedPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(seedPath));
        foreach (var spaceElement in doc.RootElement.EnumerateArray())
        {
            var equipmentArray = spaceElement.GetProperty("equipment");
            foreach (var item in equipmentArray.EnumerateArray())
            {
                var code = item.GetString();
                Assert.True(
                    EquipmentCatalog.TryNormalizeCode(code, out var canonical) && canonical == code,
                    $"Synthetic space equipment '{code}' must match a canonical catalog code.");
            }
        }
    }
}
