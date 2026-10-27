using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Unavailabilities;
using SmartCampus.Spaces.Api.Features.Unavailabilities.Models;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpaceUnavailabilityServiceTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly SpaceUnavailabilityService _service;

    public SpaceUnavailabilityServiceTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _service = new SpaceUnavailabilityService(_dbContext);
    }

    private void SetupFindCursor(IReadOnlyList<Space> spaces)
    {
        var cursor = Substitute.For<IAsyncCursor<Space>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);
        cursor.Current.Returns(spaces);

        _collection.FindAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<FindOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }

    [Fact]
    public void CreateUnavailabilityRequest_WhenEndBeforeOrEqualToStart_FailsValidation()
    {
        var start = new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

        var equalReq = new CreateUnavailabilityRequest
        {
            Start = start,
            End = start,
            Reason = "Manutenzione proiettore"
        };
        Assert.False(equalReq.TryValidateAndNormalize(out _, out var equalErr));
        Assert.Contains("strictly after", equalErr);

        var beforeReq = new CreateUnavailabilityRequest
        {
            Start = start,
            End = start.AddHours(-1),
            Reason = "Manutenzione proiettore"
        };
        Assert.False(beforeReq.TryValidateAndNormalize(out _, out var beforeErr));
        Assert.Contains("strictly after", beforeErr);
    }

    [Fact]
    public void CreateUnavailabilityRequest_ConvertsOffsetsToUtcAndValidatesReason()
    {
        var validReq = new CreateUnavailabilityRequest
        {
            Start = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(2)),
            End = new DateTimeOffset(2026, 7, 15, 12, 30, 0, TimeSpan.FromHours(2)),
            Reason = "  Verifica impianto elettrico  "
        };

        Assert.True(validReq.TryValidateAndNormalize(out var validated, out var error));
        Assert.Null(error);
        Assert.NotNull(validated);
        Assert.Equal(new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc), validated!.StartUtc);
        Assert.Equal(new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc), validated.EndUtc);
        Assert.Equal("Verifica impianto elettrico", validated.Reason);

        var emptyReasonReq = new CreateUnavailabilityRequest
        {
            Start = validReq.Start,
            End = validReq.End,
            Reason = "   "
        };
        Assert.False(emptyReasonReq.TryValidateAndNormalize(out _, out _));

        var hugeReasonReq = new CreateUnavailabilityRequest
        {
            Start = validReq.Start,
            End = validReq.End,
            Reason = new string('x', CreateUnavailabilityRequest.MaxReasonLength + 1)
        };
        Assert.False(hugeReasonReq.TryValidateAndNormalize(out _, out _));
    }

    [Fact]
    public void UnavailabilityListQuery_ValidatesPartialOrInvertedRange()
    {
        var emptyQuery = new UnavailabilityListQuery();
        Assert.True(emptyQuery.TryNormalize(out var noRange, out _));
        Assert.Null(noRange);

        var partialQuery = new UnavailabilityListQuery
        {
            From = new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero)
        };
        Assert.False(partialQuery.TryNormalize(out _, out var partialErr));
        Assert.Contains("together", partialErr);

        var invertedQuery = new UnavailabilityListQuery
        {
            From = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 10, 29, 10, 0, 0, TimeSpan.Zero)
        };
        Assert.False(invertedQuery.TryNormalize(out _, out var invertedErr));
        Assert.Contains("strictly after", invertedErr);
    }

    [Fact]
    public async Task ListBySpaceIdAsync_OrdersChronologicallyAndFiltersByOverlappingPeriod()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var space = new Space
        {
            Id = spaceId,
            Name = "Aula 101",
            Unavailabilities =
            [
                new Unavailability
                {
                    Id = "u-late",
                    Start = new DateTime(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 10, 29, 16, 0, 0, DateTimeKind.Utc),
                    Reason = "Pulizia straordinaria",
                    CreatedBy = "admin-1"
                },
                new Unavailability
                {
                    Id = "u-early",
                    Start = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc),
                    Reason = "Controllo luci",
                    CreatedBy = "admin-1"
                }
            ]
        };

        SetupFindCursor([space]);

        var all = await _service.ListBySpaceIdAsync(spaceId);
        Assert.NotNull(all);
        Assert.Equal(["u-early", "u-late"], all!.Select(u => u.Id).ToArray());

        SetupFindCursor([space]);
        var filtered = await _service.ListBySpaceIdAsync(
            spaceId,
            (
                new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc)
            ));

        Assert.NotNull(filtered);
        var match = Assert.Single(filtered!);
        Assert.Equal("u-late", match.Id);
    }

    [Fact]
    public async Task CreateAsync_PushesUnavailabilityDocumentAndUpdatesTimestamp()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        UpdateDefinition<Space>? capturedUpdate = null;

        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Do<UpdateDefinition<Space>>(u => capturedUpdate = u),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Space { Id = spaceId, Name = "Aula 101" }));

        var input = new ValidatedUnavailabilityInput(
            new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
            "Sostituzione proiettore");

        var created = await _service.CreateAsync(spaceId, input, "b7e1c2d3-0000-4000-8000-000000000001");

        Assert.NotNull(created);
        Assert.True(Guid.TryParse(created!.Id, out _));
        Assert.Equal("Sostituzione proiettore", created.Reason);
        Assert.NotNull(capturedUpdate);

        var rendered = capturedUpdate!.Render(new RenderArgs<Space>(
            BsonSerializer.SerializerRegistry.GetSerializer<Space>(),
            BsonSerializer.SerializerRegistry)).AsBsonDocument;

        Assert.True(rendered.Contains("$push"));
        Assert.Equal(
            "b7e1c2d3-0000-4000-8000-000000000001",
            rendered["$push"]["unavailabilities"]["createdBy"].AsString);
        Assert.True(rendered["$set"].AsBsonDocument.Contains("updatedAt"));
    }

    [Fact]
    public async Task DeleteAsync_DistinguishesDeletedUnavailabilityNotFoundAndSpaceNotFound()
    {
        var spaceId = Guid.NewGuid().ToString("D");
        var unavailabilityId = Guid.NewGuid().ToString("D");

        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Space { Id = spaceId }));

        var deleted = await _service.DeleteAsync(spaceId, unavailabilityId);
        Assert.Equal(DeleteUnavailabilityResult.Deleted, deleted);

        _collection.FindOneAndUpdateAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<UpdateDefinition<Space>>(),
            Arg.Any<FindOneAndUpdateOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Space>(null!));

        SetupFindCursor([new Space { Id = spaceId }]);
        var missingUnavail = await _service.DeleteAsync(spaceId, unavailabilityId);
        Assert.Equal(DeleteUnavailabilityResult.UnavailabilityNotFound, missingUnavail);

        SetupFindCursor([]);
        var missingSpace = await _service.DeleteAsync(spaceId, unavailabilityId);
        Assert.Equal(DeleteUnavailabilityResult.SpaceNotFound, missingSpace);
    }
}
