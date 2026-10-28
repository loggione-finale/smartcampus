using System.Collections;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Equipment.Models;

namespace SmartCampus.Spaces.Api.Features.Catalog.Models;

public sealed record SpaceResponse(
    string Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SpaceType Type,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SpaceMode Mode,
    string Building,
    int Floor,
    int Capacity,
    bool IsActive,
    Dictionary<string, object?> Features,
    IReadOnlyList<string> Equipment,
    IReadOnlyList<UnavailabilityResponse> Unavailabilities,
    DateTime UpdatedAt)
{
    public IReadOnlyList<EquipmentCatalogItemResponse> EquipmentDetails =>
        EquipmentCatalog.ResolveItems(Equipment);

    public static SpaceResponse From(Space space) =>
        new(
            Id: space.Id,
            Name: space.Name,
            Type: space.Type,
            Mode: space.Mode,
            Building: space.Building,
            Floor: space.Floor,
            Capacity: space.Capacity,
            IsActive: space.IsActive,
            Features: SanitizeFeatures(space.Features),
            Equipment: EquipmentCatalog.ResolveItems(space.Equipment)
                .Select(item => item.Code)
                .ToList(),
            Unavailabilities: space.Unavailabilities?
                .OrderBy(u => u.Start)
                .Select(UnavailabilityResponse.From)
                .ToList() ?? [],
            UpdatedAt: space.UpdatedAt);

    private static Dictionary<string, object?> SanitizeFeatures(Dictionary<string, object>? features)
    {
        if (features is null || features.Count == 0)
        {
            return [];
        }

        var sanitized = new Dictionary<string, object?>(features.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in features)
        {
            sanitized[key] = SanitizeValue(value);
        }

        return sanitized;
    }

    private static object? SanitizeValue(object? value)
    {
        return value switch
        {
            null or BsonNull => null,
            BsonValue bsonValue => SanitizeValue(BsonTypeMapper.MapToDotNetValue(bsonValue)),
            string str => str,
            IDictionary legacyDict => SanitizeLegacyDictionary(legacyDict),
            IEnumerable<KeyValuePair<string, object?>> kvpNullable => kvpNullable.ToDictionary(
                kvp => kvp.Key,
                kvp => SanitizeValue(kvp.Value),
                StringComparer.OrdinalIgnoreCase),
            IEnumerable enumerable => enumerable.Cast<object?>().Select(SanitizeValue).ToList(),
            _ => value
        };
    }

    private static Dictionary<string, object?> SanitizeLegacyDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(dictionary.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key?.ToString() is { } key)
            {
                result[key] = SanitizeValue(entry.Value);
            }
        }

        return result;
    }
}
