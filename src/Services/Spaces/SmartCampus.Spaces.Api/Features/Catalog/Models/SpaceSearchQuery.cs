using System.ComponentModel.DataAnnotations;
using SmartCampus.Spaces.Api.Domain;
using SmartCampus.Spaces.Api.Features.Equipment.Models;

namespace SmartCampus.Spaces.Api.Features.Catalog.Models;

public sealed class SpaceSearchQuery
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
    public const int MaxCapacity = 500;
    public const int MaxTextFilterLength = 100;
    public const int MaxListFilterItems = 20;

    [MaxLength(MaxTextFilterLength)]
    public string? Type { get; init; }

    [MaxLength(MaxTextFilterLength)]
    public string? Mode { get; init; }

    [Range(1, MaxCapacity)]
    public int? MinCapacity { get; init; }

    [MaxLength(MaxTextFilterLength)]
    public string? Building { get; init; }

    public string[]? Equipment { get; init; }

    public string[]? Software { get; init; }

    [MaxLength(MaxTextFilterLength)]
    public string? Search { get; init; }

    [Range(1, 10_000)]
    public int Page { get; init; } = DefaultPage;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    public bool IncludeInactive { get; init; } = false;

    public bool TryNormalize(out SpaceSearchFilter? filter, out string? errorDetail)
    {
        filter = null;

        SpaceType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(Type))
        {
            var rawType = Type.Trim();
            if (int.TryParse(rawType, out _)
                || !Enum.TryParse<SpaceType>(rawType, ignoreCase: true, out var spaceType)
                || !Enum.IsDefined(spaceType))
            {
                errorDetail = "Invalid 'type' filter. Allowed values are: Classroom, Lab, StudyRoom.";
                return false;
            }

            parsedType = spaceType;
        }

        SpaceMode? parsedMode = null;
        if (!string.IsNullOrWhiteSpace(Mode))
        {
            var rawMode = Mode.Trim();
            if (int.TryParse(rawMode, out _)
                || !Enum.TryParse<SpaceMode>(rawMode, ignoreCase: true, out var spaceMode)
                || !Enum.IsDefined(spaceMode))
            {
                errorDetail = "Invalid 'mode' filter. Allowed values are: Dedicated, Mixed.";
                return false;
            }

            parsedMode = spaceMode;
        }

        if (MinCapacity.HasValue && (MinCapacity.Value < 1 || MinCapacity.Value > MaxCapacity))
        {
            errorDetail = $"The 'minCapacity' parameter must be between 1 and {MaxCapacity}.";
            return false;
        }

        if (Building is not null && Building.Length > MaxTextFilterLength)
        {
            errorDetail = $"The 'building' parameter cannot exceed {MaxTextFilterLength} characters.";
            return false;
        }

        if (Search is not null && Search.Length > MaxTextFilterLength)
        {
            errorDetail = $"The 'search' parameter cannot exceed {MaxTextFilterLength} characters.";
            return false;
        }

        if (!TryNormalizeList(Equipment, "equipment", out var equipmentList, out errorDetail))
        {
            return false;
        }

        if (!TryNormalizeList(Software, "software", out var softwareList, out errorDetail))
        {
            return false;
        }

        if (Page < 1 || Page > 10_000)
        {
            errorDetail = "The 'page' parameter must be between 1 and 10000.";
            return false;
        }

        if (PageSize < 1 || PageSize > MaxPageSize)
        {
            errorDetail = $"The 'pageSize' parameter must be between 1 and {MaxPageSize}.";
            return false;
        }

        filter = new SpaceSearchFilter(
            Type: parsedType,
            Mode: parsedMode,
            MinCapacity: MinCapacity,
            Building: string.IsNullOrWhiteSpace(Building) ? null : Building.Trim(),
            Equipment: equipmentList,
            Software: softwareList,
            Search: string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
            Page: Page,
            PageSize: PageSize,
            IncludeInactive: IncludeInactive);

        errorDetail = null;
        return true;
    }

    private static bool TryNormalizeList(
        string[]? rawValues,
        string paramName,
        out IReadOnlyList<string> normalized,
        out string? errorDetail)
    {
        normalized = [];
        errorDetail = null;

        if (rawValues is null || rawValues.Length == 0)
        {
            return true;
        }

        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawEntry in rawValues)
        {
            if (string.IsNullOrWhiteSpace(rawEntry))
            {
                continue;
            }

            var parts = rawEntry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (part.Length > MaxTextFilterLength)
                {
                    errorDetail = $"Each '{paramName}' value cannot exceed {MaxTextFilterLength} characters.";
                    return false;
                }

                var normalizedPart = string.Equals(paramName, "equipment", StringComparison.Ordinal)
                    && EquipmentCatalog.TryNormalizeCode(part, out var canonicalEquipmentCode)
                    ? canonicalEquipmentCode
                    : part;

                if (seen.Add(normalizedPart))
                {
                    items.Add(normalizedPart);
                }

                if (items.Count > MaxListFilterItems)
                {
                    errorDetail = $"Cannot specify more than {MaxListFilterItems} '{paramName}' filter values.";
                    return false;
                }
            }
        }

        normalized = items;
        return true;
    }
}

public sealed record SpaceSearchFilter(
    SpaceType? Type,
    SpaceMode? Mode,
    int? MinCapacity,
    string? Building,
    IReadOnlyList<string> Equipment,
    IReadOnlyList<string> Software,
    string? Search,
    int Page = SpaceSearchQuery.DefaultPage,
    int PageSize = SpaceSearchQuery.DefaultPageSize,
    bool IncludeInactive = false);
