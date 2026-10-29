using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class MaximumAdvanceEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private HttpClient ClientWithDefaultRules()
    {
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<BookingRequestValidator>();
            services.AddSingleton(sp => new BookingRequestValidator(sp.GetRequiredService<TimeProvider>()));
        })).CreateClient();
        var token = JwtTokenHelper.GenerateToken(
            Guid.NewGuid().ToString(), $"{Guid.NewGuid():N}@smartcampus.test", SmartCampusAuthConstants.RoleUser, BookingsApiFactory.SigningKey);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static DateTimeOffset MorningOf(int daysAhead)
    {
        var day = DateTime.UtcNow.Date.AddDays(daysAhead);
        return new DateTimeOffset(DateTime.SpecifyKind(BookingTimeBands.FromCampusTime(day.AddHours(9)), DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_booking_14_days_ahead_is_accepted()
    {
        var start = MorningOf(14);

        var response = await ClientWithDefaultRules().PostAsJsonAsync(
            "/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_booking_more_than_15_days_ahead_gets_400_on_the_start_field()
    {
        var start = MorningOf(17);

        var response = await ClientWithDefaultRules().PostAsJsonAsync(
            "/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["A booking can start at most 15 days from now."], problem!.Errors["Start"]);
    }

    [Fact]
    public async Task A_booking_in_the_past_still_gets_the_past_message()
    {
        var start = MorningOf(-2);

        var response = await ClientWithDefaultRules().PostAsJsonAsync(
            "/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["The start must be in the future."], problem!.Errors["Start"]);
    }
}
