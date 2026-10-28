using System.Text.Json;

namespace SmartCampus.SyntheticData.Tests;

/// <summary>
/// The synthetic data files shared by the services, read from <c>data/synthetic</c> in the repository.
/// </summary>
public sealed class SyntheticDataSet
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string SyntheticFolder => Path.Combine(RepoRoot, "data", "synthetic");

    public JsonElement Users { get; } = Load("users.json");

    public JsonElement Spaces { get; } = Load("spaces.json");

    public JsonElement Bookings { get; } = Load("bookings.json");

    public string Readme { get; } = File.ReadAllText(Path.Combine(SyntheticFolder, "README.md"));

    public JsonElement UserById(string id) =>
        Users.EnumerateArray().Single(u => SameId(u.GetProperty("id").GetString(), id));

    public JsonElement SpaceById(string id) =>
        Spaces.EnumerateArray().Single(s => SameId(s.GetProperty("_id").GetString(), id));

    public static bool SameId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static JsonElement Load(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SyntheticFolder, fileName)));
        return document.RootElement.Clone();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "SmartCampus.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root (SmartCampus.slnx) not found.");
    }
}
