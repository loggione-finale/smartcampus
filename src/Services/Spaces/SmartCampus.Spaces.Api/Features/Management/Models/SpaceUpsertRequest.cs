using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Equipment.Models;

namespace SmartCampus.Spaces.Api.Features.Management.Models;

public abstract class SpaceUpsertRequestBase
{
    public const int MaxNameLength = 100;
    public const int MaxBuildingLength = 100;
    public const int MaxCapacity = 500;
    public const int MinFloor = -5;
    public const int MaxFloor = 50;
    public const int MaxEquipmentCount = 30;
    public const int MaxEquipmentLength = 100;
    public const int MaxFeatureKeys = 30;
    public const int MaxFeatureKeyLength = 64;
    public const int MaxFeatureStringLength = 200;
    public const int MaxFeatureArrayLength = 50;
    public const int MaxFeatureDepth = 3;

    [Required(AllowEmptyStrings = false)]
    [StringLength(MaxNameLength, MinimumLength = 1)]
    public string? Name { get; init; }

    [Required]
    public SpaceType? Type { get; init; }

    [Required]
    public SpaceMode? Mode { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(MaxBuildingLength, MinimumLength = 1)]
    public string? Building { get; init; }

    [Range(MinFloor, MaxFloor)]
    public int Floor { get; init; }

    [Required]
    [Range(1, MaxCapacity)]
    public int? Capacity { get; init; }

    public List<string>? Equipment { get; init; }

    public Dictionary<string, object?>? Features { get; init; }

    public bool TryValidateAndNormalize(out ValidatedSpaceInput? validated, out string? errorDetail)
    {
        validated = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            errorDetail = "The 'name' field is required and cannot be blank.";
            return false;
        }

        var trimmedName = Name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            errorDetail = $"The 'name' field cannot exceed {MaxNameLength} characters.";
            return false;
        }

        if (!Type.HasValue || !Enum.IsDefined(Type.Value))
        {
            errorDetail = "Invalid or missing 'type'. Allowed values are: Classroom, Lab, StudyRoom.";
            return false;
        }

        if (!Mode.HasValue || !Enum.IsDefined(Mode.Value))
        {
            errorDetail = "Invalid or missing 'mode'. Allowed values are: Dedicated, Mixed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Building))
        {
            errorDetail = "The 'building' field is required and cannot be blank.";
            return false;
        }

        var trimmedBuilding = Building.Trim();
        if (trimmedBuilding.Length > MaxBuildingLength)
        {
            errorDetail = $"The 'building' field cannot exceed {MaxBuildingLength} characters.";
            return false;
        }

        if (Floor < MinFloor || Floor > MaxFloor)
        {
            errorDetail = $"The 'floor' field must be between {MinFloor} and {MaxFloor}.";
            return false;
        }

        if (!Capacity.HasValue || Capacity.Value < 1 || Capacity.Value > MaxCapacity)
        {
            errorDetail = $"The 'capacity' field must be between 1 and {MaxCapacity}.";
            return false;
        }

        if (!TryNormalizeEquipment(Equipment, out var normalizedEquipment, out errorDetail))
        {
            return false;
        }

        if (!TryNormalizeFeatures(Features, out var normalizedFeatures, out errorDetail))
        {
            return false;
        }

        validated = new ValidatedSpaceInput(
            Name: trimmedName,
            Type: Type.Value,
            Mode: Mode.Value,
            Building: trimmedBuilding,
            Floor: Floor,
            Capacity: Capacity.Value,
            Equipment: normalizedEquipment,
            Features: normalizedFeatures);

        errorDetail = null;
        return true;
    }

    private static bool TryNormalizeEquipment(
        List<string>? rawEquipment,
        out List<string> normalized,
        out string? errorDetail) =>
        EquipmentCatalog.TryNormalizeEquipmentList(
            rawEquipment,
            allowNullAsEmpty: true,
            out normalized,
            out errorDetail);

    private static bool TryNormalizeFeatures(
        Dictionary<string, object?>? rawFeatures,
        out Dictionary<string, object> normalized,
        out string? errorDetail)
    {
        normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        errorDetail = null;

        if (rawFeatures is null || rawFeatures.Count == 0)
        {
            return true;
        }

        if (rawFeatures.Count > MaxFeatureKeys)
        {
            errorDetail = $"Cannot specify more than {MaxFeatureKeys} feature entries.";
            return false;
        }

        foreach (var (rawKey, rawValue) in rawFeatures)
        {
            if (!IsValidFeatureKey(rawKey, out errorDetail))
            {
                return false;
            }

            var key = rawKey.Trim();
            if (rawValue is null || (rawValue is JsonElement je && je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined))
            {
                continue;
            }

            if (!TryNormalizeFeatureValue(rawValue, depth: 1, out var normalizedValue, out errorDetail))
            {
                return false;
            }

            if (string.Equals(key, "workstations", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedValue is not int workstations || workstations < 1 || workstations > MaxCapacity)
                {
                    errorDetail = $"Feature 'workstations' must be an integer between 1 and {MaxCapacity}.";
                    return false;
                }
            }
            else if (string.Equals(key, "software", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedValue is not List<object?> softwareList
                    || softwareList.Any(item => item is not string str || string.IsNullOrWhiteSpace(str) || str.Length > MaxEquipmentLength))
                {
                    errorDetail = $"Feature 'software' must be an array of non-empty strings up to {MaxEquipmentLength} characters.";
                    return false;
                }
            }

            if (normalizedValue is not null)
            {
                normalized[key] = normalizedValue;
            }
        }

        return true;
    }

    private static bool IsValidFeatureKey(string? rawKey, out string? errorDetail)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            errorDetail = "Feature keys cannot be empty or whitespace.";
            return false;
        }

        var trimmed = rawKey.Trim();
        if (trimmed.Length > MaxFeatureKeyLength)
        {
            errorDetail = $"Feature keys cannot exceed {MaxFeatureKeyLength} characters.";
            return false;
        }

        if (trimmed.StartsWith('$') || trimmed.Contains('.'))
        {
            errorDetail = "Feature keys cannot start with '$' or contain '.'.";
            return false;
        }

        errorDetail = null;
        return true;
    }

    private static bool TryNormalizeFeatureValue(
        object? rawValue,
        int depth,
        out object? normalized,
        out string? errorDetail)
    {
        normalized = null;
        errorDetail = null;

        if (depth > MaxFeatureDepth)
        {
            errorDetail = $"Feature nesting cannot exceed depth {MaxFeatureDepth}.";
            return false;
        }

        switch (rawValue)
        {
            case null:
                return true;

            case JsonElement element:
                return TryNormalizeJsonElement(element, depth, out normalized, out errorDetail);

            case bool b:
                normalized = b;
                return true;

            case int i:
                normalized = i;
                return true;

            case long l:
                normalized = l is >= int.MinValue and <= int.MaxValue ? (int)l : l;
                return true;

            case double d when double.IsFinite(d):
                normalized = d;
                return true;

            case float f when float.IsFinite(f):
                normalized = (double)f;
                return true;

            case decimal dec:
                normalized = (double)dec;
                return true;

            case string str:
                if (str.Length > MaxFeatureStringLength)
                {
                    errorDetail = $"Feature string values cannot exceed {MaxFeatureStringLength} characters.";
                    return false;
                }

                normalized = str;
                return true;

            case IDictionary dict:
            {
                if (dict.Count > MaxFeatureKeys)
                {
                    errorDetail = $"Nested feature objects cannot exceed {MaxFeatureKeys} keys.";
                    return false;
                }

                var nested = new Dictionary<string, object>(dict.Count, StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in dict)
                {
                    var keyStr = entry.Key?.ToString();
                    if (!IsValidFeatureKey(keyStr, out errorDetail))
                    {
                        return false;
                    }

                    if (!TryNormalizeFeatureValue(entry.Value, depth + 1, out var childVal, out errorDetail))
                    {
                        return false;
                    }

                    if (childVal is not null)
                    {
                        nested[keyStr!.Trim()] = childVal;
                    }
                }

                normalized = nested;
                return true;
            }

            case IEnumerable enumerable:
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    if (list.Count >= MaxFeatureArrayLength)
                    {
                        errorDetail = $"Feature arrays cannot exceed {MaxFeatureArrayLength} items.";
                        return false;
                    }

                    if (!TryNormalizeFeatureValue(item, depth + 1, out var childVal, out errorDetail))
                    {
                        return false;
                    }

                    list.Add(childVal);
                }

                normalized = list;
                return true;
            }

            default:
                errorDetail = "Unsupported feature value type.";
                return false;
        }
    }

    private static bool TryNormalizeJsonElement(
        JsonElement element,
        int depth,
        out object? normalized,
        out string? errorDetail)
    {
        normalized = null;
        errorDetail = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return true;

            case JsonValueKind.True:
                normalized = true;
                return true;

            case JsonValueKind.False:
                normalized = false;
                return true;

            case JsonValueKind.String:
            {
                var str = element.GetString()!;
                if (str.Length > MaxFeatureStringLength)
                {
                    errorDetail = $"Feature string values cannot exceed {MaxFeatureStringLength} characters.";
                    return false;
                }

                normalized = str;
                return true;
            }

            case JsonValueKind.Number when element.TryGetInt32(out var i32):
                normalized = i32;
                return true;

            case JsonValueKind.Number when element.TryGetInt64(out var i64):
                normalized = i64;
                return true;

            case JsonValueKind.Number:
                normalized = element.GetDouble();
                return true;

            case JsonValueKind.Array:
            {
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    if (list.Count >= MaxFeatureArrayLength)
                    {
                        errorDetail = $"Feature arrays cannot exceed {MaxFeatureArrayLength} items.";
                        return false;
                    }

                    if (!TryNormalizeJsonElement(item, depth + 1, out var childVal, out errorDetail))
                    {
                        return false;
                    }

                    list.Add(childVal);
                }

                normalized = list;
                return true;
            }

            case JsonValueKind.Object:
            {
                if (depth >= MaxFeatureDepth)
                {
                    errorDetail = $"Feature nesting cannot exceed depth {MaxFeatureDepth}.";
                    return false;
                }

                var nested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    if (nested.Count >= MaxFeatureKeys)
                    {
                        errorDetail = $"Nested feature objects cannot exceed {MaxFeatureKeys} keys.";
                        return false;
                    }

                    if (!IsValidFeatureKey(prop.Name, out errorDetail))
                    {
                        return false;
                    }

                    if (!TryNormalizeJsonElement(prop.Value, depth + 1, out var childVal, out errorDetail))
                    {
                        return false;
                    }

                    if (childVal is not null)
                    {
                        nested[prop.Name.Trim()] = childVal;
                    }
                }

                normalized = nested;
                return true;
            }

            default:
                errorDetail = "Unsupported JSON feature value.";
                return false;
        }
    }
}

public sealed class CreateSpaceRequest : SpaceUpsertRequestBase;

public sealed class UpdateSpaceRequest : SpaceUpsertRequestBase;

public sealed record ValidatedSpaceInput(
    string Name,
    SpaceType Type,
    SpaceMode Mode,
    string Building,
    int Floor,
    int Capacity,
    List<string> Equipment,
    Dictionary<string, object> Features);
