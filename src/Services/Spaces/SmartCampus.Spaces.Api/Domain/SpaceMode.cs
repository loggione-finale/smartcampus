using System.Text.Json.Serialization;

namespace SmartCampus.Spaces.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpaceMode
{
    Dedicated,
    Mixed
}
