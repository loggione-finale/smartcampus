using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Features.Catalog;
using SmartCampus.Spaces.Api.Features.Equipment;
using SmartCampus.Spaces.Api.Features.Management;
using SmartCampus.Spaces.Api.Features.Spaces;
using SmartCampus.Spaces.Api.Features.Unavailabilities;
using SmartCampus.Spaces.Api.Infrastructure.Data;
using SmartCampus.Spaces.Api.Infrastructure.Health;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpacesMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoOptions = new MongoDbOptions();
        configuration.GetSection(MongoDbOptions.SectionName).Bind(mongoOptions);

        var connectionString = configuration.GetConnectionString("Spaces")
            ?? configuration.GetConnectionString("SpacesTest")
            ?? (!string.IsNullOrWhiteSpace(mongoOptions.ConnectionString) ? mongoOptions.ConnectionString : "mongodb://localhost:27017/spaces");

        mongoOptions.ConnectionString = connectionString;

        var mongoUrl = new MongoUrl(connectionString);
        if (!string.IsNullOrWhiteSpace(mongoUrl.DatabaseName))
        {
            mongoOptions.DatabaseName = mongoUrl.DatabaseName;
        }

        services.Configure<MongoDbOptions>(options =>
        {
            options.ConnectionString = mongoOptions.ConnectionString;
            options.DatabaseName = mongoOptions.DatabaseName;
            options.SpacesCollectionName = mongoOptions.SpacesCollectionName;
        });

        services.AddSingleton<IMongoClient>(_ =>
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            if (!connectionString.Contains("serverSelectionTimeoutMS", StringComparison.OrdinalIgnoreCase))
            {
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            }
            return new MongoClient(settings);
        });
        services.AddScoped<ISpacesDbContext, SpacesDbContext>();
        services.AddScoped<IMongoIndexInitializer, MongoIndexInitializer>();
        services.AddHostedService<MongoIndexHostedService>();
        services.AddScoped<ISpacesDataSeeder, SpacesDataSeeder>();
        services.AddHostedService<SpacesDataSeederHostedService>();
        services.AddScoped<ISpacesService, SpacesService>();
        services.AddScoped<ISpaceCatalogService, SpaceCatalogService>();
        services.AddScoped<ISpaceEquipmentService, SpaceEquipmentService>();
        services.AddScoped<ISpaceManagementService, SpaceManagementService>();
        services.AddScoped<ISpaceUnavailabilityService, SpaceUnavailabilityService>();

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: ["db", "ready"]);

        return services;
    }
}
