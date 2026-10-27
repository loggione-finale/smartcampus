using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartCampus.Spaces.Api.Domain;

[BsonIgnoreExtraElements]
public class Space
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    public SpaceType Type { get; set; }

    [BsonElement("mode")]
    [BsonRepresentation(BsonType.String)]
    public SpaceMode Mode { get; set; } = SpaceMode.Dedicated;

    [BsonElement("building")]
    public string Building { get; set; } = string.Empty;

    [BsonElement("floor")]
    public int Floor { get; set; }

    [BsonElement("capacity")]
    public int Capacity { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("features")]
    [BsonSerializer(typeof(SpaceFeaturesBsonSerializer))]
    public Dictionary<string, object> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [BsonElement("equipment")]
    public List<string> Equipment { get; set; } = [];

    [BsonElement("unavailabilities")]
    public List<Unavailability> Unavailabilities { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
