using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.MyBookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public static class BookingsServiceCollectionExtensions
{
    public static IServiceCollection AddBookings(this IServiceCollection services, IConfiguration configuration)
    {
        var spacesOptions = configuration.GetSection(SpacesOptions.SectionName).Get<SpacesOptions>() ?? new SpacesOptions();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BookingRequestValidator>();
        services.AddScoped<BookingService>();
        services.AddScoped<CancelBookingService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<MyBookingsService>();
        services.AddScoped<IPendingClosureChecker, PendingClosureChecker>();
        services.AddScoped<IAdminAlerts, AdminAlertsService>();
        services.AddSingleton<ClosingCommentValidator>();
        services.AddScoped<IClosingCommentService, ClosingCommentService>();
        services.AddHttpClient<ISpacesClient, SpacesClient>(client =>
        {
            client.BaseAddress = new Uri(spacesOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(spacesOptions.TimeoutSeconds);
        });

        return services;
    }
}
