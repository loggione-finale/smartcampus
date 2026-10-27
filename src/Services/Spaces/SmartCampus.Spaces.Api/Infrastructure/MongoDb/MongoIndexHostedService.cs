using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartCampus.Spaces.Api.Infrastructure.MongoDb;

public class MongoIndexHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MongoIndexHostedService> _logger;

    public MongoIndexHostedService(IServiceScopeFactory scopeFactory, ILogger<MongoIndexHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MongoIndexHostedService to ensure indexes exist...");
        using var scope = _scopeFactory.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IMongoIndexInitializer>();
        await initializer.InitializeIndexesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
