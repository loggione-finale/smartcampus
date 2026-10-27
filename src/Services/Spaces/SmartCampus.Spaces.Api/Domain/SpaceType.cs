using System.Text.Json.Serialization;

namespace SmartCampus.Spaces.Api.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpaceType
{
    Classroom,
    Lab,
    StudyRoom
}
