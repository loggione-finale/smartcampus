using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using SmartCampus.Spaces.Api.Infrastructure;

namespace SmartCampus.Spaces.Tests;

public class MongoConnectionSettingsTests
{
    [Fact]
    public void ServerSelectionTimeout_Defaults_To_Three_Seconds_When_Not_In_ConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Spaces"] = "mongodb://localhost:27017/spaces"
            })
            .Build();

        services.AddSpacesMongoDb(configuration);
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IMongoClient>();

        Assert.Equal(TimeSpan.FromSeconds(3), client.Settings.ServerSelectionTimeout);
    }

    [Fact]
    public void ServerSelectionTimeout_Preserves_Explicit_ServerSelectionTimeoutMS()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Spaces"] = "mongodb://localhost:27017/spaces?serverSelectionTimeoutMS=1500"
            })
            .Build();

        services.AddSpacesMongoDb(configuration);
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IMongoClient>();

        Assert.Equal(TimeSpan.FromMilliseconds(1500), client.Settings.ServerSelectionTimeout);
    }
}
