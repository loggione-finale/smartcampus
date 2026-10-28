using System.Text.Json.Serialization;
using SmartCampus.Auth;
using SmartCampus.Spaces.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddSpacesMongoDb(builder.Configuration);

var jwtSecret = builder.Configuration[SmartCampusAuthConstants.DefaultSigningKeyEnvVar]
    ?? builder.Configuration[SmartCampusAuthConstants.DefaultSigningKeyConfigKey]
    ?? builder.Configuration[SmartCampusAuthConstants.AlternateSigningKeyConfigKey]
    ?? builder.Configuration["Jwt:Key"]
    ?? (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? "development-super-secret-jwt-signing-key-for-smartcampus-123456"
        : null);

if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    builder.Services.AddSmartCampusAuth(jwtSecret, options =>
    {
        options.Issuer = builder.Configuration["Jwt:Issuer"];
        options.Audience = builder.Configuration["Jwt:Audience"];
    });
}
else
{
    builder.Services.AddSmartCampusAuth(builder.Configuration);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Spaces API v1"));
}

app.UseSmartCampusAuth();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Rende Program visibile ai progetti di test (test di integrazione)
public partial class Program { }