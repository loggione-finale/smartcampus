namespace SmartCampus.Bookings.Api.Features.Equipment;

public sealed record BookingEquipmentCatalogEntry(
    string Code,
    string Name,
    bool IsStandard,
    bool RequiresNotes);

public static class BookingEquipmentCatalog
{
    public const int MaxEquipmentItems = 20;
    public const int MaxNotesLength = 500;

    public static IReadOnlyList<BookingEquipmentCatalogEntry> All { get; } =
    [
        new("videoprojector", "Videoproiettore", IsStandard: true, RequiresNotes: false),
        new("interactive-whiteboard", "Lavagna interattiva", IsStandard: true, RequiresNotes: false),
        new("audio-kit", "Kit audio", IsStandard: true, RequiresNotes: false),
        new("microphone", "Microfono", IsStandard: true, RequiresNotes: false),
        new("other", "Altro", IsStandard: false, RequiresNotes: true)
    ];

    private static readonly Dictionary<string, BookingEquipmentCatalogEntry> ByCode =
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
        ["lavagna"] = "interactive-whiteboard",
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

    public static bool TryResolve(string? rawValue, out BookingEquipmentCatalogEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (Aliases.TryGetValue(trimmed, out var canonicalCode)
            && ByCode.TryGetValue(canonicalCode, out var found))
        {
            entry = found;
            return true;
        }

        return false;
    }

    public static string GetDisplayName(string codeOrRaw) =>
        TryResolve(codeOrRaw, out var entry) && entry is not null
            ? entry.Name
            : codeOrRaw;
}
