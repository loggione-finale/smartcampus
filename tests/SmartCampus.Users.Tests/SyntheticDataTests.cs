using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Data.Seed;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Domain.Enums;

namespace SmartCampus.Users.Tests;

public class SyntheticDataTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "SmartCampus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SmartCampus.Users.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Users_Json_Contains_Only_Synthetic_Data_And_Documented_Domains()
    {
        var usersPath = Path.Combine(RepoRoot, "data", "synthetic", "users.json");
        Assert.True(File.Exists(usersPath), "users.json must exist in data/synthetic");

        await using var stream = File.OpenRead(usersPath);
        using var doc = await JsonDocument.ParseAsync(stream);
        var users = doc.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(users);

        foreach (var user in users)
        {
            var email = user.GetProperty("email").GetString()!;
            Assert.EndsWith("@smartcampus.test", email);
            Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("matricola").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("demoPassword").GetString()));
            Assert.True(Guid.TryParse(user.GetProperty("id").GetString(), out _));
        }
    }

    [Fact]
    public async Task Spaces_Json_Is_Valid_And_Aligned_With_DataModel()
    {
        var spacesPath = Path.Combine(RepoRoot, "data", "synthetic", "spaces.json");
        Assert.True(File.Exists(spacesPath), "spaces.json must exist in data/synthetic");

        await using var stream = File.OpenRead(spacesPath);
        using var doc = await JsonDocument.ParseAsync(stream);
        var spaces = doc.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(spaces);

        foreach (var space in spaces)
        {
            Assert.True(Guid.TryParse(space.GetProperty("_id").GetString(), out _));
            var type = space.GetProperty("type").GetString()!;
            Assert.Contains(type, new[] { "Classroom", "Lab", "StudyRoom" });
            Assert.True(space.GetProperty("capacity").GetInt32() > 0);

            if (space.TryGetProperty("unavailabilities", out var unavailabilities))
            {
                foreach (var unav in unavailabilities.EnumerateArray())
                {
                    var start = unav.GetProperty("start").GetDateTime();
                    var end = unav.GetProperty("end").GetDateTime();
                    Assert.True(end > start, "Unavailability end must be after start");
                }
            }
        }
    }

    [Fact]
    public async Task Bookings_Json_References_Existing_Users_And_Spaces()
    {
        var bookingsPath = Path.Combine(RepoRoot, "data", "synthetic", "bookings.json");
        var usersPath = Path.Combine(RepoRoot, "data", "synthetic", "users.json");
        var spacesPath = Path.Combine(RepoRoot, "data", "synthetic", "spaces.json");

        Assert.True(File.Exists(bookingsPath));

        await using var bStream = File.OpenRead(bookingsPath);
        using var bDoc = await JsonDocument.ParseAsync(bStream);

        await using var uStream = File.OpenRead(usersPath);
        using var uDoc = await JsonDocument.ParseAsync(uStream);

        await using var sStream = File.OpenRead(spacesPath);
        using var sDoc = await JsonDocument.ParseAsync(sStream);

        var canonicalUserIds = uDoc.RootElement.EnumerateArray()
            .Select(u => u.GetProperty("id").GetString()!)
            .ToHashSet();

        var canonicalSpaceIds = sDoc.RootElement.EnumerateArray()
            .Select(s => s.GetProperty("_id").GetString()!)
            .ToHashSet();

        // Check users defined in bookings file match canonical user IDs
        foreach (var user in bDoc.RootElement.GetProperty("users").EnumerateArray())
        {
            Assert.Contains(user.GetProperty("id").GetString()!, canonicalUserIds);
        }

        // Check spaces defined in bookings file match canonical space IDs
        foreach (var space in bDoc.RootElement.GetProperty("spaces").EnumerateArray())
        {
            Assert.Contains(space.GetProperty("id").GetString()!, canonicalSpaceIds);
        }
    }

    [Fact]
    public async Task UsersDbSeeder_Seeds_Canonical_Users_And_Is_Idempotent()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase($"SeederTest_{Guid.NewGuid()}")
            .Options;

        await using var db = new UsersDbContext(options);
        var passwordHasher = new PasswordHasher<UserProfile>();

        var environment = new TestHostEnvironment(Environments.Development);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Seed:AdminPassword", "AdminPass123" },
                { "Seed:DefaultUserPassword", "Password123" }
            })
            .Build();

        var seeder = new UsersDbSeeder(db, passwordHasher, config, environment);

        // 1. First execution: seeds users
        await seeder.SeedAsync();

        var users = await db.UserProfiles.ToListAsync();
        Assert.Equal(6, users.Count);

        var admin = users.Single(u => u.Role == UserRole.Admin);
        Assert.Equal("admin@smartcampus.test", admin.Email);
        Assert.Equal("00000001", admin.Matricola);
        Assert.Equal(Guid.Parse("b7e1c2d3-0000-4000-8000-000000000001"), admin.Id);
        Assert.Equal(PasswordVerificationResult.Success, passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, "AdminPass123"));

        var elena = users.Single(u => u.Email == "elena.greco@smartcampus.test");
        Assert.Equal(Guid.Parse("b7e1c2d3-0000-4000-8000-000000000006"), elena.Id);
        Assert.Equal(PasswordVerificationResult.Success, passwordHasher.VerifyHashedPassword(elena, elena.PasswordHash, "Password123"));

        // 2. Second execution: idempotent, does not add duplicates
        await seeder.SeedAsync();
        Assert.Equal(6, await db.UserProfiles.CountAsync());
    }
}
