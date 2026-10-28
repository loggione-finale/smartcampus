using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.MyBookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Api.Features.Bookings.Users;
using SmartCampus.Bookings.Api.Features.Invitations;
using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public static class BookingsServiceCollectionExtensions
{
    public static IServiceCollection AddBookings(this IServiceCollection services, IConfiguration configuration)
    {
        var spacesOptions = configuration.GetSection(SpacesOptions.SectionName).Get<SpacesOptions>() ?? new SpacesOptions();
        var usersOptions = configuration.GetSection(UsersOptions.SectionName).Get<UsersOptions>() ?? new UsersOptions();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BookingRequestValidator>();
        services.AddScoped<BookingService>();
        services.AddScoped<CancelBookingService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<MyBookingsService>();
        services.AddScoped<AcceptInvitationService>();
        services.AddScoped<MyInvitationsService>();
        services.AddScoped<IPendingClosureChecker, PendingClosureChecker>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<AdminCancelBookingService>();
        services.AddHttpClient<ISpacesClient, SpacesClient>(client =>
        {
            client.BaseAddress = new Uri(spacesOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(spacesOptions.TimeoutSeconds);
        });
        services.AddHttpClient<IUsersClient, UsersClient>(client =>
        {
            client.BaseAddress = new Uri(usersOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(usersOptions.TimeoutSeconds);
        });

        return services;
    }
}
