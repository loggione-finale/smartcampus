using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Users.Api.Data;

namespace SmartCampus.Users.Tests.Support;

public class UsersApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public const string TestJwtSigningKey = "super-secret-jwt-signing-key-for-smartcampus-tests-123456";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Users", "Host=localhost;Database=users_test" },
                { "JWT_SECRET", TestJwtSigningKey },
                { "Jwt:SigningKey", TestJwtSigningKey }
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UsersDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<UsersDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }
}
