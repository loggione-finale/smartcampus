using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Data.Seed;
using SmartCampus.Bookings.Api.Features.Bookings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Bookings")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Bookings'. Set it with dotnet user-secrets.");

builder.Services.AddDbContext<BookingsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
        .UseSnakeCaseNamingConvention());

builder.Services.AddSmartCampusAuth(builder.Configuration);
builder.Services.AddBookings(builder.Configuration);
builder.Services.AddScoped<BookingsSeeder>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookingsDbContext>("postgres", failureStatus: HealthStatus.Unhealthy);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();

        if (app.Environment.IsDevelopment())
        {
            await scope.ServiceProvider.GetRequiredService<BookingsSeeder>().SeedIfEmptyAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Bookings API v1"));
}

app.UseSmartCampusAuth();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Rende Program visibile ai progetti di test (test di integrazione)
public partial class Program { }