using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class TimeZoneTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private async Task<(DateTime Start, DateTime End)> CreateAsync(DateTimeOffset start, DateTimeOffset end)
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, end));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (booking.GetProperty("start").GetDateTime().ToUniversalTime(), booking.GetProperty("end").GetDateTime().ToUniversalTime());
    }

    [Theory]
    [InlineData(2027, 3, 28, 8)]
    [InlineData(2027, 10, 31, 9)]
    [InlineData(2027, 7, 14, 8)]
    [InlineData(2027, 12, 14, 9)]
    public async Task A_booking_at_10_in_Rome_stays_at_10_in_Rome_on_every_day_of_the_year(int year, int month, int day, int expectedUtcHour)
    {
        var romeStart = new DateTime(year, month, day, 10, 0, 0, DateTimeKind.Unspecified);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(romeStart, Rome), TimeSpan.Zero);

        var created = await CreateAsync(startUtc, startUtc.AddHours(2));

        Assert.Equal(expectedUtcHour, created.Start.Hour);
        Assert.Equal(startUtc.UtcDateTime, created.Start);
        var backInRome = TimeZoneInfo.ConvertTimeFromUtc(created.Start, Rome);
        Assert.Equal(10, backInRome.Hour);
        Assert.Equal(0, backInRome.Minute);
    }

    [Theory]
    [InlineData(2027, 3, 28)]
    [InlineData(2027, 10, 31)]
    public async Task A_booking_across_the_clock_change_night_keeps_its_real_duration(int year, int month, int day)
    {
        var start = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);

        var created = await CreateAsync(start, start.AddHours(3));

        Assert.Equal(TimeSpan.FromHours(3), created.End - created.Start);
    }

    [Fact]
    public async Task Offsets_other_than_UTC_are_rejected()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = new DateTimeOffset(2027, 3, 28, 10, 0, 0, TimeSpan.FromHours(2));

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
