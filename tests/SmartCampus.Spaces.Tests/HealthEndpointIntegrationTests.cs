using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmartCampus.Spaces.Api.Infrastructure.MongoDb;

namespace SmartCampus.Spaces.Tests;

public class HealthEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_Returns_503_When_Database_Is_Unreachable()
    {
        var mockDbContext = Substitute.For<ISpacesDbContext>();
        var mockDb = Substitute.For<IMongoDatabase>();
        mockDb.RunCommandAsync<BsonDocument>(
            Arg.Any<Command<BsonDocument>>(),
            Arg.Any<ReadPreference>(),
            Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("Database down"));
        mockDbContext.Database.Returns(mockDb);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => mockDbContext);
            });
        }).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Unhealthy", content);
    }

    [Fact]
    public async Task HealthEndpoint_Returns_200_When_Database_Is_Reachable()
    {
        var mockDbContext = Substitute.For<ISpacesDbContext>();
        var mockDb = Substitute.For<IMongoDatabase>();
        mockDb.RunCommandAsync<BsonDocument>(
            Arg.Any<Command<BsonDocument>>(),
            Arg.Any<ReadPreference>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BsonDocument("ok", 1)));
        mockDbContext.Database.Returns(mockDb);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => mockDbContext);
            });
        }).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }
}
