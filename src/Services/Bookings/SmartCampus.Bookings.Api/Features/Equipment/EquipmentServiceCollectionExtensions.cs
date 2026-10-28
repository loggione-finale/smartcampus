using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Equipment;

public static class EquipmentServiceCollectionExtensions
{
    public static IServiceCollection AddEquipment(this IServiceCollection services, IConfiguration configuration)
    {
        var spacesOptions = configuration.GetSection(SpacesOptions.SectionName).Get<SpacesOptions>() ?? new SpacesOptions();

        services.TryAddScoped<IAdminAlerts, AdminAlertsService>();
        services.AddScoped<IBookingEquipmentService, BookingEquipmentService>();
        services.AddHttpClient<ISpaceEquipmentClient, SpaceEquipmentClient>(client =>
        {
            client.BaseAddress = new Uri(spacesOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(spacesOptions.TimeoutSeconds);
        });

        return services;
    }
}
