using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class DurationAndTimeBandTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static DateTimeOffset At(DateTimeOffset day, int hour, int minute = 0)
    {
        var local = new DateTime(day.UtcDateTime.Year, day.UtcDateTime.Month, day.UtcDateTime.Day, hour, minute, 0);
        return new DateTimeOffset(BookingTimeBands.FromCampusTime(local), TimeSpan.Zero);
    }

    private async Task<HttpResponseMessage> BookAsync(HttpClient client, DateTimeOffset start, DateTimeOffset end, Guid? spaceId = null) =>
        await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId ?? Guid.NewGuid(), start, end));

    [Theory]
    [InlineData(9, 0, 9, 15, HttpStatusCode.BadRequest)]
    [InlineData(9, 0, 13, 15, HttpStatusCode.BadRequest)]
    [InlineData(9, 0, 9, 30, HttpStatusCode.Created)]
    [InlineData(9, 0, 13, 0, HttpStatusCode.Created)]
    public async Task The_duration_must_be_between_30_minutes_and_4_hours(int fromHour, int fromMinute, int toHour, int toMinute, HttpStatusCode expected)
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await BookAsync(client, At(day, fromHour, fromMinute), At(day, toHour, toMinute));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(13, 15, "straddling morning and afternoon")]
    [InlineData(18, 30, "straddling afternoon and evening")]
    [InlineData(7, 0, "before the first band")]
    [InlineData(22, 30, "ending after the evening band")]
    public async Task A_booking_must_fit_in_a_single_time_band(int fromHour, int fromMinute, string reason)
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await BookAsync(client, At(day, fromHour, fromMinute), At(day, fromHour, fromMinute).AddHours(1));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, reason);
    }

    [Theory]
    [InlineData(8, 0, 9, 0)]
    [InlineData(13, 0, 14, 0)]
    [InlineData(14, 0, 15, 0)]
    [InlineData(18, 0, 19, 0)]
    [InlineData(19, 0, 20, 0)]
    [InlineData(22, 0, 23, 0)]
    public async Task Bookings_at_the_edges_of_a_band_are_accepted(int fromHour, int fromMinute, int toHour, int toMinute)
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await BookAsync(client, At(day, fromHour, fromMinute), At(day, toHour, toMinute));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_same_organizer_can_book_once_per_band_on_the_same_day()
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var morning = await BookAsync(client, At(day, 9), At(day, 10));
        var afternoon = await BookAsync(client, At(day, 15), At(day, 16));
        var evening = await BookAsync(client, At(day, 20), At(day, 21));

        Assert.Equal(HttpStatusCode.Created, morning.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afternoon.StatusCode);
        Assert.Equal(HttpStatusCode.Created, evening.StatusCode);
    }

    [Fact]
    public async Task A_second_booking_in_the_same_band_is_rejected_even_without_overlap()
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await BookAsync(client, At(day, 9), At(day, 10));

        var response = await BookAsync(client, At(day, 11), At(day, 12));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Time band already used", problem!.Title);
    }

    [Fact]
    public async Task The_same_band_on_another_day_is_accepted()
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await BookAsync(client, At(day, 9), At(day, 10));

        var response = await BookAsync(client, At(day.AddDays(1), 9), At(day.AddDays(1), 10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_limit_does_not_apply_to_different_organizers()
    {
        var day = TestSlots.Next();
        var spaceId = Guid.NewGuid();
        var first = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await BookAsync(first, At(day, 9), At(day, 10), spaceId);

        var response = await BookAsync(second, At(day, 10), At(day, 11), spaceId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task After_the_cancellation_the_band_is_free_again_for_the_same_organizer()
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var created = await BookAsync(client, At(day, 9), At(day, 10));
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        var blocked = await BookAsync(client, At(day, 11), At(day, 12));

        await client.PostAsync($"/bookings/{bookingId}/cancel", null);
        var afterCancel = await BookAsync(client, At(day, 11), At(day, 12));

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterCancel.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_requests_of_the_same_organizer_in_the_same_band_let_only_one_through()
    {
        var day = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            BookAsync(client, At(day, 9 + index % 4), At(day, 9 + index % 4).AddMinutes(30))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
