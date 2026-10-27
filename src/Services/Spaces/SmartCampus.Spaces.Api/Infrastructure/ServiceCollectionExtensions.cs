using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
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

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<ISpacesDbContext, SpacesDbContext>();
        services.AddScoped<IMongoIndexInitializer, MongoIndexInitializer>();
        services.AddHostedService<MongoIndexHostedService>();
        services.AddScoped<SmartCampus.Spaces.Api.Infrastructure.Data.ISpacesDataSeeder, SmartCampus.Spaces.Api.Infrastructure.Data.SpacesDataSeeder>();
        services.AddHostedService<SmartCampus.Spaces.Api.Infrastructure.Data.SpacesDataSeederHostedService>();

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: ["db", "ready"]);

        return services;
    }
}
