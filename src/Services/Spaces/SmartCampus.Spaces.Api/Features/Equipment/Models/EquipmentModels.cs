using System.ComponentModel.DataAnnotations;

namespace SmartCampus.Spaces.Api.Features.Equipment.Models;

public sealed record EquipmentCatalogItemResponse(
    string Code,
    string Name,
    string Description,
    bool IsStandard,
    bool RequiresNotes);

public static class EquipmentCatalog
{
    public const int MaxEquipmentCount = 30;
    public const int MaxEquipmentLength = 100;

    public static IReadOnlyList<EquipmentCatalogItemResponse> All { get; } =
    [
        new(
            Code: "videoprojector",
            Name: "Videoproiettore",
            Description: "Videoproiettore per presentazioni, lezioni e riunioni di gruppo.",
            IsStandard: true,
            RequiresNotes: false),
        new(
            Code: "interactive-whiteboard",
            Name: "Lavagna interattiva",
            Description: "Lavagna multimediale interattiva (LIM) per lavoro collaborativo.",
            IsStandard: true,
            RequiresNotes: false),
        new(
            Code: "audio-kit",
            Name: "Kit audio",
            Description: "Impianto audio portatile con altoparlanti per presentazioni ed eventi.",
            IsStandard: true,
            RequiresNotes: false),
        new(
            Code: "microphone",
            Name: "Microfono",
            Description: "Microfono wireless o da tavolo per conferenze e interventi in aula.",
            IsStandard: true,
            RequiresNotes: false),
        new(
            Code: "other",
            Name: "Altro",
            Description: "Altro strumento specifico richiedibile per attività di gruppo (specificare nelle note).",
            IsStandard: false,
            RequiresNotes: true)
    ];

    private static readonly Dictionary<string, EquipmentCatalogItemResponse> ByCode =
        All.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["videoprojector"] = "videoprojector",
        ["video-projector"] = "videoprojector",
        ["video_projector"] = "videoprojector",
        ["videoproiettore"] = "videoprojector",
        ["projector"] = "videoprojector",

        ["interactive-whiteboard"] = "interactive-whiteboard",
        ["interactive_whiteboard"] = "interactive-whiteboard",
        ["interactive whiteboard"] = "interactive-whiteboard",
        ["lavagna interattiva"] = "interactive-whiteboard",
        ["lavagna-interattiva"] = "interactive-whiteboard",
        ["lim"] = "interactive-whiteboard",

        ["audio-kit"] = "audio-kit",
        ["audio_kit"] = "audio-kit",
        ["audio kit"] = "audio-kit",
        ["audiokit"] = "audio-kit",
        ["kit audio"] = "audio-kit",
        ["kit-audio"] = "audio-kit",

        ["microphone"] = "microphone",
        ["microfono"] = "microphone",

        ["other"] = "other",
        ["altro"] = "other"
    };

    public static string AllowedCodesDescription { get; } =
        string.Join(", ", All.Select(item => item.Code));

    public static bool TryNormalizeCode(string? rawValue, out string canonicalCode)
    {
        canonicalCode = string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (Aliases.TryGetValue(trimmed, out var matched))
        {
            canonicalCode = matched;
            return true;
        }

        return false;
    }

    public static bool TryGetItem(string? codeOrAlias, out EquipmentCatalogItemResponse? item)
    {
        if (TryNormalizeCode(codeOrAlias, out var canonicalCode)
            && ByCode.TryGetValue(canonicalCode, out var found))
        {
            item = found;
            return true;
        }

        item = null;
        return false;
    }

    public static IReadOnlyList<EquipmentCatalogItemResponse> ResolveItems(IEnumerable<string>? equipmentCodes)
    {
        if (equipmentCodes is null)
        {
            return [];
        }

        var resolved = new List<EquipmentCatalogItemResponse>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in equipmentCodes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (TryGetItem(raw, out var catalogItem) && catalogItem is not null)
            {
                if (seen.Add(catalogItem.Code))
                {
                    resolved.Add(catalogItem);
                }
            }
            else
            {
                var trimmed = raw.Trim();
                if (seen.Add(trimmed))
                {
                    resolved.Add(new EquipmentCatalogItemResponse(
                        Code: trimmed,
                        Name: trimmed,
                        Description: string.Empty,
                        IsStandard: false,
                        RequiresNotes: false));
                }
            }
        }

        return resolved;
    }

    public static bool TryNormalizeEquipmentList(
        IReadOnlyList<string>? rawEquipment,
        bool allowNullAsEmpty,
        out List<string> normalized,
        out string? errorDetail)
    {
        normalized = [];
        errorDetail = null;

        if (rawEquipment is null)
        {
            if (allowNullAsEmpty)
            {
                return true;
            }

            errorDetail = "The 'equipment' field is required (use an empty array [] to clear equipment).";
            return false;
        }

        if (rawEquipment.Count == 0)
        {
            return true;
        }

        if (rawEquipment.Count > MaxEquipmentCount)
        {
            errorDetail = $"Cannot specify more than {MaxEquipmentCount} equipment items.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawEquipment)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                errorDetail = "Equipment items cannot be empty or whitespace.";
                return false;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length > MaxEquipmentLength)
            {
                errorDetail = $"Each equipment item cannot exceed {MaxEquipmentLength} characters.";
                return false;
            }

            if (!TryNormalizeCode(trimmed, out var canonicalCode))
            {
                errorDetail = $"Unknown equipment item '{trimmed}'. Allowed catalog codes are: {AllowedCodesDescription}.";
                return false;
            }

            if (seen.Add(canonicalCode))
            {
                normalized.Add(canonicalCode);
            }
        }

        return true;
    }
}

public sealed class UpdateSpaceEquipmentRequest
{
    [Required]
    public List<string>? Equipment { get; init; }

    public bool TryValidateAndNormalize(
        out List<string> normalizedEquipment,
        out string? errorDetail) =>
        EquipmentCatalog.TryNormalizeEquipmentList(
            Equipment,
            allowNullAsEmpty: false,
            out normalizedEquipment,
            out errorDetail);
}
