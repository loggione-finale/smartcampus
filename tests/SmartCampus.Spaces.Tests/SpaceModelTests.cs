using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Tests;

public class SpaceModelTests
{
    [Fact]
    public void Space_Initializes_With_Expected_Defaults()
    {
        var space = new Space();

        Assert.False(string.IsNullOrWhiteSpace(space.Id));
        Assert.True(Guid.TryParse(space.Id, out _));
        Assert.Equal(SpaceMode.Dedicated, space.Mode);
        Assert.True(space.IsActive);
        Assert.Empty(space.Features);
        Assert.Empty(space.Equipment);
        Assert.Empty(space.Unavailabilities);
        Assert.True((DateTime.UtcNow - space.CreatedAt).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - space.UpdatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Space_Serializes_And_Deserializes_To_Bson_Accurately()
    {
        var original = new Space
        {
            Id = "3f2a9c1e-7b4d-4e8a-9c1f-2d5e6a7b8c9d",
            Name = "Laboratorio Informatica 2",
            Type = SpaceType.Lab,
            Mode = SpaceMode.Dedicated,
            Building = "Edificio B",
            Floor = 1,
            Capacity = 24,
            IsActive = true,
            Features = new Dictionary<string, object>
            {
                ["workstations"] = 24,
                ["software"] = new[] { "Visual Studio Code", "Docker" },
                ["projector"] = true
            },
            Equipment = ["videoprojector", "interactive-whiteboard"],
            Unavailabilities =
            [
                new Unavailability
                {
                    Id = "8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
                    Start = new DateTime(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc),
                    End = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
                    Reason = "Manutenzione impianto elettrico",
                    CreatedBy = "b7e1c2d3-0000-4000-8000-000000000002"
                }
            ],
            CreatedAt = new DateTime(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc)
        };

        // Serialize to BSON
        var bsonDoc = original.ToBsonDocument();

        Assert.Equal("3f2a9c1e-7b4d-4e8a-9c1f-2d5e6a7b8c9d", bsonDoc["_id"].AsString);
        Assert.Equal("Laboratorio Informatica 2", bsonDoc["name"].AsString);
        Assert.Equal("Lab", bsonDoc["type"].AsString);
        Assert.Equal("Dedicated", bsonDoc["mode"].AsString);
        Assert.Equal("Edificio B", bsonDoc["building"].AsString);
        Assert.Equal(1, bsonDoc["floor"].AsInt32);
        Assert.Equal(24, bsonDoc["capacity"].AsInt32);
        Assert.True(bsonDoc["isActive"].AsBoolean);
        Assert.Equal(2, bsonDoc["equipment"].AsBsonArray.Count);
        Assert.Single(bsonDoc["unavailabilities"].AsBsonArray);

        // Deserialize back
        var deserialized = BsonSerializer.Deserialize<Space>(bsonDoc);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Mode, deserialized.Mode);
        Assert.Equal(original.Building, deserialized.Building);
        Assert.Equal(original.Floor, deserialized.Floor);
        Assert.Equal(original.Capacity, deserialized.Capacity);
        Assert.Equal(original.IsActive, deserialized.IsActive);
        Assert.Equal(original.Equipment, deserialized.Equipment);
        Assert.Single(deserialized.Unavailabilities);
        Assert.Equal(original.Unavailabilities[0].Reason, deserialized.Unavailabilities[0].Reason);
        Assert.Equal(original.Unavailabilities[0].Start, deserialized.Unavailabilities[0].Start);
        Assert.Equal(original.Unavailabilities[0].End, deserialized.Unavailabilities[0].End);
    }
}
