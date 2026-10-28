using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using SmartCampus.Auth;
using SmartCampus.Users.Api.Data;
using SmartCampus.Users.Api.Data.Seed;
using SmartCampus.Users.Api.Domain.Entities;
using SmartCampus.Users.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Users")
        ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Users'. Set it with dotnet user-secrets.");

    var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
    if (!connectionString.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
    {
        connectionStringBuilder.Timeout = 3;
    }

    builder.Services.AddDbContext<UsersDbContext>(options =>
        options.UseNpgsql(
                connectionStringBuilder.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 2, maxRetryDelay: TimeSpan.FromSeconds(1), errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention());
}

builder.Services.AddExceptionHandler<DatabaseUnavailableExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<UsersDbContext>("database", failureStatus: HealthStatus.Unhealthy);

builder.Services.AddScoped<IPasswordHasher<UserProfile>, PasswordHasher<UserProfile>>();
builder.Services.AddScoped<UsersDbSeeder>();

var jwtSecret = builder.Configuration[SmartCampusAuthConstants.DefaultSigningKeyEnvVar]
    ?? builder.Configuration[SmartCampusAuthConstants.DefaultSigningKeyConfigKey]
    ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? "development-super-secret-jwt-signing-key-for-smartcampus-123456"
        : null);

if (!string.IsNullOrEmpty(jwtSecret))
{
    builder.Services.AddSmartCampusAuth(jwtSecret);
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<UsersDbSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Users API v1"));
}

app.UseExceptionHandler();
app.UseSmartCampusAuth();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expose Program class to integration test projects
public partial class Program { }
