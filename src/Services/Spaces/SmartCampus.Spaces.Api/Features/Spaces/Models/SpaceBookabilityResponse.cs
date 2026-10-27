using System.Text.Json.Serialization;
using SmartCampus.Spaces.Api.Domain;

namespace SmartCampus.Spaces.Api.Features.Spaces.Models;

public record SpaceBookabilityResponse(
    bool Bookable,
    string? Reason,
    string Name,
    string Type,
    string Building,
    int Capacity,
    [property: JsonPropertyName("accessMode")]
    SpaceMode AccessMode);
