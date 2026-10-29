using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Data.Seed;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;

namespace SmartCampus.SyntheticData.Tests;

/// <summary>
/// Each service owns its database, so shared ids and copies (users, spaces) must match by hand.
/// These tests fail when the synthetic data of one service drifts from the others (#147, #158, #160).
/// Every test collects all the differences, so one run shows everything that needs fixing.
/// </summary>
public partial class SyntheticDataConsistencyTests
{
    private const string EmailDomain = "@smartcampus.test";

    private readonly SyntheticDataSet data = new();

    [Fact]
    public void Users_follow_the_PO_rules()
    {
        var problems = new List<string>();
        var users = data.Users.EnumerateArray().ToList();

        foreach (var user in users)
        {
            var id = user.GetProperty("id").GetString();
            var matricola = user.GetProperty("matricola").GetString() ?? "";
            var email = user.GetProperty("email").GetString() ?? "";

            if (!TenDigits().IsMatch(matricola))
            {
                problems.Add($"user {id}: matricola '{matricola}' must have exactly 10 digits (PO decision 19)");
            }

            if (!email.EndsWith(EmailDomain, StringComparison.Ordinal) || email != email.ToLowerInvariant())
            {
                problems.Add($"user {id}: email '{email}' must be lowercase and end with {EmailDomain}");
            }
        }

        AddDuplicates(problems, "user id", users.Select(u => u.GetProperty("id").GetString()!.ToLowerInvariant()));
        AddDuplicates(problems, "email", users.Select(u => u.GetProperty("email").GetString()!));
        AddDuplicates(problems, "matricola", users.Select(u => u.GetProperty("matricola").GetString()!));

        if (!users.Any(u => u.GetProperty("role").GetString() == UserRole.Admin))
        {
            problems.Add("users.json has no Admin account");
        }

        Assert.Empty(problems);
    }

    [Fact]
    public async Task Users_seeder_loads_the_same_users_as_users_json()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase($"SyntheticData_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new UsersDbContext(options);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = "AdminPass123",
                ["Seed:DefaultUserPassword"] = "Password123"
            })
            .Build();

        await new UsersDbSeeder(dbContext, new PasswordHasher<UserProfile>(), configuration, new DevelopmentEnvironment())
            .SeedAsync();
        var seeded = await dbContext.UserProfiles.ToListAsync();

        var problems = new List<string>();
        var expected = data.Users.EnumerateArray().ToList();
        if (seeded.Count != expected.Count)
        {
            problems.Add($"UsersDbSeeder loads {seeded.Count} users, users.json has {expected.Count}");
        }

        foreach (var user in expected)
        {
            var id = Guid.Parse(user.GetProperty("id").GetString()!);
            var profile = seeded.SingleOrDefault(p => p.Id == id);
            if (profile is null)
            {
                problems.Add($"user {id} is in users.json but not in UsersDbSeeder");
                continue;
            }

            Compare(problems, $"user {id}", "email", user.GetProperty("email").GetString(), profile.Email);
            Compare(problems, $"user {id}", "matricola", user.GetProperty("matricola").GetString(), profile.Matricola);
            Compare(problems, $"user {id}", "displayName", user.GetProperty("displayName").GetString(), profile.DisplayName);
            Compare(problems, $"user {id}", "role", user.GetProperty("role").GetString(), profile.Role);
            Compare(problems, $"user {id}", "phoneNumber", user.GetProperty("phoneNumber").GetString(), profile.PhoneNumber);
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Users_in_the_bookings_seed_match_users_json()
    {
        var problems = new List<string>();

        foreach (var user in data.Bookings.GetProperty("users").EnumerateArray())
        {
            var id = user.GetProperty("id").GetString()!;
            if (!data.Users.EnumerateArray().Any(u => SyntheticDataSet.SameId(u.GetProperty("id").GetString(), id)))
            {
                problems.Add($"bookings.json user {id} does not exist in users.json");
                continue;
            }

            var canonical = data.UserById(id);
            Compare(problems, $"bookings.json user {id}", "email", canonical.GetProperty("email").GetString(), user.GetProperty("email").GetString());
            Compare(problems, $"bookings.json user {id}", "displayName", canonical.GetProperty("displayName").GetString(), user.GetProperty("displayName").GetString());
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Spaces_have_unique_ids_and_names_and_cover_every_type_and_several_buildings()
    {
        var spaces = data.Spaces.EnumerateArray().ToList();
        var ids = spaces.Select(s => s.GetProperty("_id").GetString()!.ToLowerInvariant()).ToList();
        var names = spaces.Select(s => s.GetProperty("name").GetString()!).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(spaces.Count >= 15, $"spaces.json has {spaces.Count} spaces, expected at least 15");

        var types = spaces.Select(s => s.GetProperty("type").GetString()).Distinct().ToList();
        Assert.Contains("Classroom", types);
        Assert.Contains("Lab", types);
        Assert.Contains("StudyRoom", types);

        var buildings = spaces.Select(s => s.GetProperty("building").GetString()).Distinct().ToList();
        Assert.True(buildings.Count >= 5, $"spaces.json uses {buildings.Count} buildings, expected at least 5");
    }

    [Fact]
    public void Spaces_in_the_bookings_seed_match_spaces_json()
    {
        var problems = new List<string>();

        foreach (var space in data.Bookings.GetProperty("spaces").EnumerateArray())
        {
            var id = space.GetProperty("id").GetString()!;
            if (!data.Spaces.EnumerateArray().Any(s => SyntheticDataSet.SameId(s.GetProperty("_id").GetString(), id)))
            {
                problems.Add($"bookings.json space {id} does not exist in spaces.json");
                continue;
            }

            var canonical = data.SpaceById(id);
            var label = $"bookings.json space {id}";
            Compare(problems, label, "name", canonical.GetProperty("name").GetString(), space.GetProperty("name").GetString());
            Compare(problems, label, "type", canonical.GetProperty("type").GetString(), space.GetProperty("type").GetString());
            Compare(problems, label, "building", canonical.GetProperty("building").GetString(), space.GetProperty("building").GetString());
            Compare(problems, label, "mode/accessMode", canonical.GetProperty("mode").GetString(), space.GetProperty("accessMode").GetString());
            Compare(problems, label, "capacity", canonical.GetProperty("capacity").GetInt32().ToString(), space.GetProperty("capacity").GetInt32().ToString());
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Bookings_reference_existing_users_and_spaces_and_respect_capacity()
    {
        var problems = new List<string>();
        var userKeys = data.Bookings.GetProperty("users").EnumerateArray()
            .Select(u => u.GetProperty("key").GetString()!).ToHashSet();
        var spaces = data.Bookings.GetProperty("spaces").EnumerateArray()
            .ToDictionary(s => s.GetProperty("key").GetString()!, s => s.GetProperty("capacity").GetInt32());
        var bookings = data.Bookings.GetProperty("bookings").EnumerateArray().ToList();

        foreach (var booking in bookings)
        {
            var code = booking.GetProperty("referenceCode").GetString();
            var spaceKey = booking.GetProperty("space").GetString()!;

            if (!spaces.TryGetValue(spaceKey, out var capacity))
            {
                problems.Add($"{code}: space '{spaceKey}' is not in bookings.json spaces");
                continue;
            }

            if (!userKeys.Contains(booking.GetProperty("organizer").GetString()!))
            {
                problems.Add($"{code}: organizer '{booking.GetProperty("organizer").GetString()}' is not in bookings.json users");
            }

            if (booking.TryGetProperty("seatsRequested", out var seats) && seats.GetInt32() > capacity)
            {
                problems.Add($"{code}: {seats.GetInt32()} seats requested, capacity is {capacity}");
            }

            if (booking.TryGetProperty("closingComment", out var comment)
                && comment.TryGetProperty("attendeesCount", out var attendees)
                && attendees.GetInt32() > capacity)
            {
                problems.Add($"{code}: closing comment declares {attendees.GetInt32()} attendees, capacity is {capacity}");
            }

            if (!booking.TryGetProperty("invitations", out var invitations))
            {
                continue;
            }

            foreach (var invitation in invitations.EnumerateArray())
            {
                if (invitation.TryGetProperty("user", out var user) && !userKeys.Contains(user.GetString()!))
                {
                    problems.Add($"{code}: invited user '{user.GetString()}' is not in bookings.json users");
                }

                if (invitation.TryGetProperty("email", out var email)
                    && !email.GetString()!.EndsWith(EmailDomain, StringComparison.Ordinal))
                {
                    problems.Add($"{code}: invited email '{email.GetString()}' must end with {EmailDomain}");
                }
            }
        }

        AddDuplicates(problems, "referenceCode", bookings.Select(b => b.GetProperty("referenceCode").GetString()!));
        Assert.Empty(problems);
    }

    [Fact]
    public void Space_unavailabilities_are_created_by_an_existing_admin()
    {
        var problems = new List<string>();

        foreach (var space in data.Spaces.EnumerateArray())
        {
            foreach (var unavailability in space.GetProperty("unavailabilities").EnumerateArray())
            {
                var createdBy = unavailability.GetProperty("createdBy").GetString();
                var author = data.Users.EnumerateArray()
                    .FirstOrDefault(u => SyntheticDataSet.SameId(u.GetProperty("id").GetString(), createdBy));

                if (author.ValueKind == JsonValueKind.Undefined)
                {
                    problems.Add($"space {space.GetProperty("_id").GetString()}: unavailability created by {createdBy}, who is not in users.json");
                }
                else if (author.GetProperty("role").GetString() != UserRole.Admin)
                {
                    problems.Add($"space {space.GetProperty("_id").GetString()}: unavailability created by {createdBy}, who is not an Admin");
                }
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Readme_tables_match_the_json_files()
    {
        var problems = new List<string>();

        foreach (var user in data.Users.EnumerateArray())
        {
            var id = user.GetProperty("id").GetString()!;
            var row = data.Readme.Split('\n').FirstOrDefault(line => line.Contains($"`{id}`", StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                problems.Add($"README: no row for user {id}");
                continue;
            }

            ExpectInRow(problems, row, $"user {id}", $"`{user.GetProperty("matricola").GetString()}`");
            ExpectInRow(problems, row, $"user {id}", $"`{user.GetProperty("email").GetString()}`");
        }

        foreach (var space in data.Spaces.EnumerateArray())
        {
            var id = space.GetProperty("_id").GetString()!;
            var row = data.Readme.Split('\n').FirstOrDefault(line => line.Contains($"`{id}`", StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                problems.Add($"README: no row for space {id}");
                continue;
            }

            ExpectInRow(problems, row, $"space {id}", $"`{space.GetProperty("type").GetString()}`");
            ExpectInRow(problems, row, $"space {id}", $"`{space.GetProperty("mode").GetString()}`");
            ExpectInRow(problems, row, $"space {id}", $"| {space.GetProperty("building").GetString()} |");
            ExpectInRow(problems, row, $"space {id}", $"| {space.GetProperty("capacity").GetInt32()} |");
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Each_synthetic_file_exists_only_once()
    {
        var shared = new[] { "users.json", "spaces.json", "bookings.json", "synthetic-bookings.json", "synthetic-spaces.json", "synthetic-users.json" };
        var excluded = new[] { $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}" };

        var copies = new[] { "src", "tests" }
            .Select(folder => Path.Combine(SyntheticDataSet.RepoRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            .Where(file => shared.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !excluded.Any(part => file.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(file => Path.GetRelativePath(SyntheticDataSet.RepoRoot, file))
            .ToList();

        // The services link the files of data/synthetic from their .csproj: a real copy in src/ or tests/ drifts (#158)
        Assert.Empty(copies);
    }

    private static void Compare(List<string> problems, string label, string field, string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            problems.Add($"{label}: {field} is '{actual}', expected '{expected}'");
        }
    }

    private static void ExpectInRow(List<string> problems, string row, string label, string expected)
    {
        if (!row.Contains(expected, StringComparison.Ordinal))
        {
            problems.Add($"README {label}: row does not contain {expected}");
        }
    }

    private static void AddDuplicates(List<string> problems, string field, IEnumerable<string> values)
    {
        foreach (var duplicate in values.GroupBy(v => v).Where(g => g.Count() > 1))
        {
            problems.Add($"duplicate {field}: {duplicate.Key}");
        }
    }

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex TenDigits();

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SmartCampus.SyntheticData.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
