using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Features.Registration;
using SmartCampus.Users.Tests.Support;

namespace SmartCampus.Users.Tests;

public class RegistrationEndpointTests : IClassFixture<UsersApiFactory>
{
    private readonly UsersApiFactory _factory;

    public RegistrationEndpointTests(UsersApiFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest CreateValidRequest(string? matricola = null, string? email = null) => new(
        Matricola: matricola ?? $"{Random.Shared.Next(10000000, 99999999)}",
        FirstName: "Marco",
        LastName: "Neri",
        Email: email ?? $"user.{Guid.NewGuid():N}@smartcampus.test",
        PhoneNumber: "+393339876543",
        Password: "SecurePassword1"
    );

    [Fact]
    public async Task Register_WithValidData_Returns201Created_AndNeverExposesPassword()
    {
        var client = _factory.CreateClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();
        // Critical: no password or password hash must ever be exposed in the response payload
        Assert.DoesNotContain("password", rawContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", rawContent, StringComparison.OrdinalIgnoreCase);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(request.Matricola, user.Matricola);
        Assert.Equal(request.Email.ToLowerInvariant(), user.Email);
        Assert.Equal("Marco Neri", user.DisplayName);
        Assert.Equal("User", user.Role);

        // Verify that the user is persisted in the database with a hashed password
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var savedUser = await db.UserProfiles.FindAsync(user.Id);
        Assert.NotNull(savedUser);
        Assert.NotEmpty(savedUser.PasswordHash);
        Assert.NotEqual(request.Password, savedUser.PasswordHash);
    }

    [Fact]
    public async Task Register_WithInvalidData_Returns400BadRequest_WithValidationDetails()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(
            Matricola: "123", // invalid < 6 digits
            FirstName: "A", // invalid < 2 chars
            LastName: "", // invalid empty
            Email: "not-an-email", // invalid
            PhoneNumber: "3331234567", // invalid missing +39
            Password: "short" // invalid < 10
        );

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawContent);
        var errors = doc.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty(nameof(request.Matricola), out _));
        Assert.True(errors.TryGetProperty(nameof(request.FirstName), out _));
        Assert.True(errors.TryGetProperty(nameof(request.LastName), out _));
        Assert.True(errors.TryGetProperty(nameof(request.Email), out _));
        Assert.True(errors.TryGetProperty(nameof(request.PhoneNumber), out _));
        Assert.True(errors.TryGetProperty(nameof(request.Password), out _));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var existingEmail = $"duplicate.{Guid.NewGuid():N}@smartcampus.test";
        var firstRequest = CreateValidRequest(matricola: "88888881", email: existingEmail);
        var secondRequest = CreateValidRequest(matricola: "88888882", email: existingEmail);

        var firstResponse = await client.PostAsJsonAsync("/auth/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/auth/register", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var rawContent = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("Email is already registered", rawContent);
    }

    [Fact]
    public async Task Register_WithDuplicateMatricola_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var existingMatricola = $"{Random.Shared.Next(10000000, 99999999)}";
        var firstRequest = CreateValidRequest(matricola: existingMatricola, email: $"email1.{Guid.NewGuid():N}@smartcampus.test");
        var secondRequest = CreateValidRequest(matricola: existingMatricola, email: $"email2.{Guid.NewGuid():N}@smartcampus.test");

        var firstResponse = await client.PostAsJsonAsync("/auth/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/auth/register", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var rawContent = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("Matricola is already registered", rawContent);
    }

    [Fact]
    public async Task GetUsers_WithEmailsFilter_ReturnsMatchingUsers()
    {
        var client = _factory.CreateClient();
        var user1 = CreateValidRequest(email: $"target1.{Guid.NewGuid():N}@smartcampus.test");
        var user2 = CreateValidRequest(email: $"target2.{Guid.NewGuid():N}@smartcampus.test");

        await client.PostAsJsonAsync("/auth/register", user1);
        await client.PostAsJsonAsync("/auth/register", user2);

        var response = await client.GetAsync($"/users?emails={user1.Email},{user2.Email}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Email == user1.Email.ToLowerInvariant());
        Assert.Contains(users, u => u.Email == user2.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task GetUsers_WithoutEmailsFilter_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/users");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var emptyResponse = await client.GetAsync("/users?emails=");
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);

        var whitespaceResponse = await client.GetAsync("/users?emails=   ");
        Assert.Equal(HttpStatusCode.BadRequest, whitespaceResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_WithExistingUser_Returns200Ok()
    {
        var client = _factory.CreateClient();
        var request = CreateValidRequest();

        var createResponse = await client.PostAsJsonAsync("/auth/register", request);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(createdUser);

        var response = await client.GetAsync($"/users/{createdUser.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(createdUser.Id, user.Id);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404NotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_ReturnsResponse()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seeder_PreloadsAdminAndSyntheticUsers_WithValidPasswordHash()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserProfile>>();

        var admin = await db.UserProfiles.FirstOrDefaultAsync(u => u.Email == "admin@smartcampus.test");
        Assert.NotNull(admin);
        Assert.Equal("Admin", admin.Role);
        var verificationResult = hasher.VerifyHashedPassword(admin, admin.PasswordHash, "AdminPass123");
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        var students = await db.UserProfiles.Where(u => u.Role == "User" && u.Email.EndsWith("@smartcampus.test")).ToListAsync();
        Assert.NotEmpty(students);
    }
}
