using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Api.Features.Equipment;

namespace SmartCampus.Bookings.Tests.Support;

public class BookingsApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-signing-key-with-at-least-32-bytes!";

    public FakeSpacesClient Spaces { get; } = new();

    public FakeSpaceEquipmentClient SpaceEquipment { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BookingsTest")
            ?? throw new InvalidOperationException("Set the ConnectionStrings__BookingsTest environment variable.");

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Bookings", connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISpacesClient>();
            services.AddSingleton<ISpacesClient>(Spaces);
            services.RemoveAll<ISpaceEquipmentClient>();
            services.AddSingleton<ISpaceEquipmentClient>(SpaceEquipment);
        });
    }

    public HttpClient CreateAuthenticatedClient(
        Guid userId,
        string email = "student@smartcampus.test",
        string role = SmartCampusAuthConstants.RoleUser)
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(userId.ToString(), email, role, SigningKey);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
