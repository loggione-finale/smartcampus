using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartCampus.Auth;

namespace SmartCampus.Users.Tests;

public class SmartCampusAuthTests
{
    private const string ValidSigningKey = "super-secret-jwt-signing-key-for-smartcampus-tests-123456";
    private const string DifferentSigningKey = "another-completely-different-signing-key-for-tests-654321";

    private static async Task<HttpClient> CreateTestClientAsync(
        string signingKey = ValidSigningKey,
        Action<SmartCampusAuthOptions>? configureOptions = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSmartCampusAuth(signingKey, configureOptions);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseSmartCampusAuth();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/public", () => "public").AllowAnonymous();
                        endpoints.MapGet("/api/protected", () => "protected").RequireAuthorization();
                        endpoints.MapGet("/api/admin-only", () => "admin-only")
                            .RequireAuthorization(SmartCampusAuthConstants.PolicyAdmin);
                    });
                });
            })
            .StartAsync();

        return host.GetTestClient();
    }

    [Fact]
    public async Task Request_WithoutToken_ToProtectedEndpoint_Returns401Unauthorized()
    {
        using var client = await CreateTestClientAsync();

        var response = await client.GetAsync("/api/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithoutToken_ToAdminEndpoint_Returns401Unauthorized()
    {
        using var client = await CreateTestClientAsync();

        var response = await client.GetAsync("/api/admin-only");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithExpiredToken_Returns401Unauthorized()
    {
        using var client = await CreateTestClientAsync();

        var expiredToken = JwtTokenHelper.GenerateToken(
            userId: "user-1",
            email: "user@smartcampus.local",
            role: SmartCampusAuthConstants.RoleUser,
            signingKey: ValidSigningKey,
            lifetime: TimeSpan.FromMinutes(-10));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithTamperedToken_Returns401Unauthorized()
    {
        using var client = await CreateTestClientAsync();

        // Signed with a different key
        var tamperedToken = JwtTokenHelper.GenerateToken(
            userId: "user-1",
            email: "user@smartcampus.local",
            role: SmartCampusAuthConstants.RoleUser,
            signingKey: DifferentSigningKey);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync("/api/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithMalformedToken_Returns401Unauthorized()
    {
        using var client = await CreateTestClientAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt-token");

        var response = await client.GetAsync("/api/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithUserRole_ToAdminEndpoint_Returns403Forbidden()
    {
        using var client = await CreateTestClientAsync();

        var userToken = JwtTokenHelper.GenerateToken(
            userId: "user-regular",
            email: "student@smartcampus.local",
            role: SmartCampusAuthConstants.RoleUser,
            signingKey: ValidSigningKey);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.GetAsync("/api/admin-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithAdminRole_ToAdminEndpoint_Returns200Ok()
    {
        using var client = await CreateTestClientAsync();

        var adminToken = JwtTokenHelper.GenerateToken(
            userId: "admin-1",
            email: "admin@smartcampus.local",
            role: SmartCampusAuthConstants.RoleAdmin,
            signingKey: ValidSigningKey);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("admin-only", content);
    }

    [Fact]
    public async Task Request_WithUserRole_ToGeneralProtectedEndpoint_Returns200Ok()
    {
        using var client = await CreateTestClientAsync();

        var userToken = JwtTokenHelper.GenerateToken(
            userId: "user-regular",
            email: "student@smartcampus.local",
            role: SmartCampusAuthConstants.RoleUser,
            signingKey: ValidSigningKey);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.GetAsync("/api/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("protected", content);
    }

    [Fact]
    public async Task Request_WithoutToken_ToPublicEndpoint_Returns200Ok()
    {
        using var client = await CreateTestClientAsync();

        var response = await client.GetAsync("/api/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("public", content);
    }

    [Fact]
    public void AddSmartCampusAuth_WithoutConfiguredKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSmartCampusAuth(emptyConfig);
        });

        Assert.Contains("JWT signing key is not configured", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSmartCampusAuth_WithBlankKey_ThrowsArgumentException(string blankKey)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
        {
            services.AddSmartCampusAuth(blankKey);
        });
    }

    [Fact]
    public void AddSmartCampusAuth_WithTooShortKey_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            services.AddSmartCampusAuth("short-key");
        });

        Assert.Contains("at least 16 bytes", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void JwtTokenHelper_GenerateToken_WithBlankKey_ThrowsArgumentException(string blankKey)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            JwtTokenHelper.GenerateToken("id", "a@b.com", SmartCampusAuthConstants.RoleUser, blankKey);
        });
    }

    [Fact]
    public void JwtTokenHelper_GenerateToken_WithTooShortKey_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            JwtTokenHelper.GenerateToken("id", "a@b.com", SmartCampusAuthConstants.RoleUser, "short-key");
        });

        Assert.Contains("at least 16 bytes", exception.Message);
    }

    [Fact]
    public async Task AddSmartCampusAuth_WithConfiguration_LoadsKeySuccessfully()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JWT_SECRET", ValidSigningKey }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSmartCampusAuth(config);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseSmartCampusAuth();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/admin-check", () => "ok")
                            .RequireAuthorization(SmartCampusAuthConstants.PolicyAdmin);
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var token = JwtTokenHelper.GenerateToken("id", "a@b.com", SmartCampusAuthConstants.RoleAdmin, ValidSigningKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin-check");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
