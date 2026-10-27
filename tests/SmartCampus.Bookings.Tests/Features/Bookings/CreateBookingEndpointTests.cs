using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class CreateBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static DateTimeOffset NextSlot(int hour = 10)
    {
        var day = DateTimeOffset.UtcNow.Date.AddDays(60 + Random.Shared.Next(0, 3000));
        return new DateTimeOffset(day.AddHours(hour), TimeSpan.Zero);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), NextSlot(), NextSlot().AddHours(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creates_a_booking_with_the_organizer_taken_from_the_token()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = NextSlot();
        var client = factory.CreateAuthenticatedClient(userId);

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(userId, booking.GetProperty("organizerId").GetGuid());
        Assert.Equal(spaceId, booking.GetProperty("spaceId").GetGuid());
        Assert.Equal("Confirmed", booking.GetProperty("status").GetString());
        Assert.Equal("Laboratorio Informatica 2", booking.GetProperty("spaceName").GetString());
        Assert.Matches("^SC-\\d{6}-[A-Z2-9]{4}$", booking.GetProperty("referenceCode").GetString()!);
        Assert.Equal(start.UtcDateTime, booking.GetProperty("start").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Rejects_an_end_before_the_start_with_400()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = NextSlot();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.Contains("End", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Rejects_a_slot_in_the_past_with_400()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-3).AddHours(10), TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_times_not_aligned_to_15_minutes_with_400()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = NextSlot().AddMinutes(7);

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_409_when_the_dedicated_space_is_already_booked()
    {
        var spaceId = Guid.NewGuid();
        var start = NextSlot();
        var first = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await first.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(2)));

        var response = await second.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start.AddHours(1), start.AddHours(3)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Space already booked", problem!.Title);
    }

    [Fact]
    public async Task Accepts_adjacent_bookings_on_the_same_space()
    {
        var spaceId = Guid.NewGuid();
        var start = NextSlot();
        var first = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await first.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        var response = await second.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start.AddHours(1), start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Returns_409_when_the_organizer_has_an_overlapping_booking()
    {
        var start = NextSlot();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start.AddHours(1), start.AddHours(3)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Overlapping booking", problem!.Title);
    }

    [Fact]
    public async Task Returns_503_when_Spaces_is_unreachable()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? throw new SpacesUnavailableException("down") : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = NextSlot();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_when_the_space_does_not_exist()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? null : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = NextSlot();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_409_with_the_reason_when_the_space_is_not_bookable()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId
            ? FakeSpacesClient.DefaultSpace() with { Bookable = false, Reason = "Under maintenance" }
            : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = NextSlot();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Under maintenance", problem!.Detail);
    }

    [Fact]
    public async Task Returns_409_with_the_code_to_comment_when_a_closing_comment_is_pending()
    {
        var userId = Guid.NewGuid();
        var pendingCode = await SeedFinishedBookingAsync(userId);
        var client = factory.CreateAuthenticatedClient(userId);
        var start = NextSlot();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(pendingCode, problem.GetProperty("bookingToComment").GetString());
    }

    private async Task<string> SeedFinishedBookingAsync(Guid organizerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var end = DateTime.UtcNow.AddDays(-1);
        var code = $"SC-000000-{Random.Shared.Next(1000, 9999)}";

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            ReferenceCode = code,
            SpaceId = Guid.NewGuid(),
            SpaceNameSnapshot = "Aula 1",
            SpaceTypeSnapshot = "Classroom",
            SpaceBuildingSnapshot = "Edificio A",
            SpaceAccessModeSnapshot = SpaceAccessMode.Dedicated,
            SpaceCapacitySnapshot = 30,
            OrganizerId = organizerId,
            OrganizerNameSnapshot = "student@smartcampus.test",
            Period = new NpgsqlRange<DateTime>(end.AddHours(-2), true, end, false),
            SeatsRequested = 1,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

        return code;
    }
}
