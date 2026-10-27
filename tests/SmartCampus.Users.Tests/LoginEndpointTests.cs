using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartCampus.Auth;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Features.Login;
using SmartCampus.Users.Api.Features.Registration;
using SmartCampus.Users.Tests.Support;

namespace SmartCampus.Users.Tests;

public class LoginEndpointTests : IClassFixture<UsersApiFactory>
{
    private readonly UsersApiFactory _factory;

    public LoginEndpointTests(UsersApiFactory factory)
    {
        _factory = factory;
    }

    private static RegisterRequest CreateValidRegisterRequest(string? matricola = null, string? email = null, string? password = null) => new(
        Matricola: matricola ?? $"{Random.Shared.NextInt64(1000000000L, 9999999999L)}",
        FirstName: "Giuseppe",
        LastName: "Verdi",
        Email: email ?? $"user.{Guid.NewGuid():N}@smartcampus.test",
        PhoneNumber: "+393339876543",
        Password: password ?? "SecurePassword1"
    );

    [Fact]
    public async Task Login_WithValidEmail_Returns200Ok_AndValidJwtToken()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        var regResponse = await client.PostAsJsonAsync("/auth/register", reg);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var loginRequest = new LoginRequest(Username: reg.Email, Password: reg.Password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", rawContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", rawContent, StringComparison.OrdinalIgnoreCase);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(28800, result.ExpiresIn); // 8 hours in seconds
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(reg.Email.ToLowerInvariant(), result.User.Email);
        Assert.Equal(reg.Matricola, result.User.Matricola);
        Assert.Equal("User", result.User.Role);

        // Verify that the token is cryptographically valid and signed with the expected key
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(SigningKeyValidator.Validate(UsersApiFactory.TestJwtSigningKey, "key")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(result.Token, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);

        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(result.User.Id.ToString(), subClaim);

        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value;
        Assert.Equal("User", roleClaim);

        var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        Assert.Equal(reg.Email.ToLowerInvariant(), emailClaim);
    }

    [Fact]
    public async Task Login_WithValidMatricola_Returns200Ok_AndValidJwtToken()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        var regResponse = await client.PostAsJsonAsync("/auth/register", reg);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Login using matricola instead of email
        var loginRequest = new LoginRequest(Username: reg.Matricola, Password: reg.Password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal(reg.Matricola, result.User.Matricola);
        Assert.Equal(reg.Email.ToLowerInvariant(), result.User.Email);
    }

    [Fact]
    public async Task Login_WithAlternativeFieldNames_Returns200Ok()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        await client.PostAsJsonAsync("/auth/register", reg);

        // Identifier property
        var reqWithIdentifier = new LoginRequest(Identifier: reg.Email, Password: reg.Password);
        var resp1 = await client.PostAsJsonAsync("/auth/login", reqWithIdentifier);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // EmailOrMatricola property
        var reqWithEmailOrMatricola = new LoginRequest(EmailOrMatricola: reg.Matricola, Password: reg.Password);
        var resp2 = await client.PostAsJsonAsync("/auth/login", reqWithEmailOrMatricola);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
    }

    [Fact]
    public async Task Login_WithCaseInsensitiveEmail_Returns200Ok()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        await client.PostAsJsonAsync("/auth/register", reg);

        var uppercaseEmail = reg.Email.ToUpperInvariant();
        var loginRequest = new LoginRequest(Username: uppercaseEmail, Password: reg.Password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsAdminRole()
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(Username: "admin@smartcampus.test", Password: "AdminPass123");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.Equal("Admin", result.User.Role);

        // Verify token carries Admin role claim
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(result.Token);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        Assert.Equal("Admin", roleClaim);
    }

    [Fact]
    public async Task Login_WithSeededAdminMatricola_ReturnsAdminRole()
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(Username: "0000000001", Password: "AdminPass123");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.Equal("Admin", result.User.Role);
        Assert.Equal("0000000001", result.User.Matricola);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401Unauthorized_WithGenericMessage()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        await client.PostAsJsonAsync("/auth/register", reg);

        var loginRequest = new LoginRequest(Username: reg.Email, Password: "WrongPassword999");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawContent);
        var detail = doc.RootElement.GetProperty("detail").GetString();
        Assert.Equal("Invalid credentials.", detail);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401Unauthorized_WithGenericMessage()
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(Username: "nonexistent.user@smartcampus.test", Password: "Password123");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var rawContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawContent);
        var detail = doc.RootElement.GetProperty("detail").GetString();
        // Crucial security requirement: message must not reveal whether the username was found or not
        Assert.Equal("Invalid credentials.", detail);
    }

    [Fact]
    public async Task Login_WithInactiveUser_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var reg = CreateValidRegisterRequest();
        var regResp = await client.PostAsJsonAsync("/auth/register", reg);
        var createdUser = await regResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(createdUser);

        // Deactivate user in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.UserProfiles.FindAsync(createdUser.Id);
            Assert.NotNull(user);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var loginRequest = new LoginRequest(Username: reg.Email, Password: reg.Password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Password123")]
    [InlineData("   ", "Password123")]
    [InlineData("user@smartcampus.test", "")]
    [InlineData("user@smartcampus.test", "   ")]
    public async Task Login_WithMissingFields_Returns400BadRequest(string username, string password)
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(Username: username, Password: password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
