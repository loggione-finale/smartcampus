using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Users;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Api.Features.Equipment;
using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Tests.Support;

public class BookingsApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-signing-key-with-at-least-32-bytes!";

    public FakeSpacesClient Spaces { get; } = new();

    public FakeSpaceEquipmentClient SpaceEquipment { get; } = new();

    public FakeUsersClient Users { get; } = new();

    /// <summary>
    /// Cartella pickup usata dall'app in test: i .eml finiscono qui, non nella cwd.
    /// </summary>
    public string PickupDirectory { get; } = Path.Combine(
        Path.GetTempPath(), "smartcampus-tests-outbox", Guid.NewGuid().ToString("N"));

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
            services.RemoveAll<BookingRequestValidator>();
            services.AddSingleton(sp => new BookingRequestValidator(sp.GetRequiredService<TimeProvider>(), TimeSpan.FromDays(10000)));
            services.RemoveAll<ISpaceEquipmentClient>();
            services.AddSingleton<ISpaceEquipmentClient>(SpaceEquipment);
            services.RemoveAll<IUsersClient>();
            services.AddSingleton<IUsersClient>(Users);
            services.Configure<NotificationOptions>(options =>
                options.PickupDirectoryLocation = PickupDirectory);
        });
    }

    public HttpClient CreateAuthenticatedClient(
        Guid userId,
        string email = "student@smartcampus.test",
        string role = SmartCampusAuthConstants.RoleUser,
        string? displayName = null)
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(
            userId.ToString(), email, role, SigningKey, name: displayName);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
