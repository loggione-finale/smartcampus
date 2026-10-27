using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartCampus.Spaces.Api.Domain;

public class Unavailability
{
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Start { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime End { get; set; }

    public string Reason { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public string CreatedBy { get; set; } = string.Empty;
}
