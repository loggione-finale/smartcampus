using MongoDB.Driver;
using NSubstitute;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Spaces;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class SpacesServiceTests
{
    private readonly ISpacesDbContext _dbContext = Substitute.For<ISpacesDbContext>();
    private readonly IMongoCollection<Space> _collection = Substitute.For<IMongoCollection<Space>>();
    private readonly SpacesService _service;

    public SpacesServiceTests()
    {
        _dbContext.Spaces.Returns(_collection);
        _service = new SpacesService(_dbContext);
    }

    private void SetupSpaceInDb(Space? space)
    {
        var cursor = Substitute.For<IAsyncCursor<Space>>();
        var items = space is not null ? new List<Space> { space } : new List<Space>();
        cursor.Current.Returns(items);
        cursor.MoveNext(Arg.Any<CancellationToken>()).Returns(items.Count > 0, false);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(items.Count > 0), Task.FromResult(false));

        _collection.FindAsync(
            Arg.Any<FilterDefinition<Space>>(),
            Arg.Any<FindOptions<Space, Space>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }

    [Fact]
    public async Task GetBookabilityAsync_WhenSpaceNotFound_ReturnsNull()
    {
        SetupSpaceInDb(null);

        var result = await _service.GetBookabilityAsync(
            "non-existent-id",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBookabilityAsync_WhenSpaceIsInactive_ReturnsBookableFalseWithReason()
    {
        var space = new Space
        {
            Id = "space-1",
            Name = "Aula 101",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio A",
            Capacity = 30,
            IsActive = false
        };
        SetupSpaceInDb(space);

        var result = await _service.GetBookabilityAsync(
            "space-1",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2));

        Assert.NotNull(result);
        Assert.False(result.Bookable);
        Assert.Equal("Lo spazio è disattivato", result.Reason);
        Assert.Equal("Aula 101", result.Name);
        Assert.Equal("Classroom", result.Type);
        Assert.Equal("Edificio A", result.Building);
        Assert.Equal(30, result.Capacity);
        Assert.Equal(SpaceMode.Dedicated, result.AccessMode);
    }

    [Fact]
    public async Task GetBookabilityAsync_WhenSpaceIsActiveAndNoUnavailabilities_ReturnsBookableTrue()
    {
        var space = new Space
        {
            Id = "space-2",
            Name = "Laboratorio Informatica 2",
            Type = SpaceType.Lab,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio B",
            Capacity = 24,
            IsActive = true,
            Unavailabilities = []
        };
        SetupSpaceInDb(space);

        var from = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 28, 11, 0, 0, DateTimeKind.Utc);

        var result = await _service.GetBookabilityAsync("space-2", from, to);

        Assert.NotNull(result);
        Assert.True(result.Bookable);
        Assert.Null(result.Reason);
        Assert.Equal("Laboratorio Informatica 2", result.Name);
        Assert.Equal("Lab", result.Type);
        Assert.Equal("Edificio B", result.Building);
        Assert.Equal(24, result.Capacity);
        Assert.Equal(SpaceMode.Dedicated, result.AccessMode);
    }

    [Fact]
    public async Task GetBookabilityAsync_WhenSpaceHasOverlappingUnavailability_ReturnsBookableFalseWithReason()
    {
        var space = new Space
        {
            Id = "space-3",
            Name = "Sala Studio Piano Terra",
            Type = SpaceType.StudyRoom,
            Mode = SpaceMode.Mixed,
            Building = "Edificio C",
            Capacity = 12,
            IsActive = true,
            Unavailabilities =
            [
                new Unavailability
                {
                    Start = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
                    Reason = "Manutenzione impianto elettrico"
                }
            ]
        };
        SetupSpaceInDb(space);

        var from = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 28, 13, 0, 0, DateTimeKind.Utc);

        var result = await _service.GetBookabilityAsync("space-3", from, to);

        Assert.NotNull(result);
        Assert.False(result.Bookable);
        Assert.Equal("Manutenzione impianto elettrico", result.Reason);
        Assert.Equal(SpaceMode.Mixed, result.AccessMode);
    }

    [Fact]
    public async Task GetBookabilityAsync_WhenUnavailabilityIsAdjacent_ReturnsBookableTrue()
    {
        var space = new Space
        {
            Id = "space-4",
            Name = "Aula 204",
            Type = SpaceType.Classroom,
            Mode = SpaceMode.Mixed,
            Building = "Edificio A",
            Capacity = 6,
            IsActive = true,
            Unavailabilities =
            [
                new Unavailability
                {
                    Start = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc),
                    Reason = "Pulizie straordinarie"
                }
            ]
        };
        SetupSpaceInDb(space);

        // Requested interval [10:00, 12:00) starts exactly when unavailability ends
        var from = new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        var result = await _service.GetBookabilityAsync("space-4", from, to);

        Assert.NotNull(result);
        Assert.True(result.Bookable);
        Assert.Null(result.Reason);
    }
}
