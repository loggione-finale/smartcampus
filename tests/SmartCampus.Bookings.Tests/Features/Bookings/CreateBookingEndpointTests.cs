using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class CreateBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), TestSlots.Next(), TestSlots.Next().AddHours(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creates_a_booking_with_the_organizer_taken_from_the_token()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
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
        var start = TestSlots.Next();

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
        var start = TestSlots.Next().AddMinutes(7);

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_409_when_the_dedicated_space_is_already_booked()
    {
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
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
        var start = TestSlots.Next();
        var first = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await first.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        var response = await second.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start.AddHours(1), start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Returns_409_when_the_organizer_books_twice_in_the_same_time_band()
    {
        var start = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start.AddHours(1), start.AddHours(3)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Time band already used", problem!.Title);
    }

    [Fact]
    public async Task Returns_503_when_Spaces_is_unreachable()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? throw new SpacesUnavailableException("down") : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_when_the_space_does_not_exist()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? null : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next();

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
        var start = TestSlots.Next();

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
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(pendingCode, problem.GetProperty("bookingToComment").GetString());
    }

    [Fact]
    public async Task An_admin_creating_a_booking_gets_403_and_nothing_is_saved()
    {
        var adminId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(adminId, role: SmartCampusAuthConstants.RoleAdmin);
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Admins cannot create bookings", problem!.Title);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var bookings = await dbContext.Bookings.Where(b => b.OrganizerId == adminId).ToListAsync();
        Assert.Empty(bookings);
    }

    [Fact]
    public async Task Ignores_forbidden_fields_in_the_request_body()
    {
        var userId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var start = TestSlots.Next();
        var client = factory.CreateAuthenticatedClient(userId);
        var rawBody = new
        {
            spaceId = Guid.NewGuid(),
            start,
            end = start.AddHours(1),
            organizerId = attackerId,
            organizerEmail = "attacker@evil.test",
            status = "Cancelled",
            referenceCode = "SC-000000-HACK"
        };

        var response = await client.PostAsync(
            "/bookings",
            new StringContent(JsonSerializer.Serialize(rawBody, JsonOptions), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(userId, booking.GetProperty("organizerId").GetGuid());
        Assert.Equal("Confirmed", booking.GetProperty("status").GetString());
        Assert.Matches("^SC-\\d{6}-[A-Z2-9]{4}$", booking.GetProperty("referenceCode").GetString()!);
        Assert.NotEqual("SC-000000-HACK", booking.GetProperty("referenceCode").GetString());
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
            BookingDate = DateOnly.FromDateTime(end),
            TimeBand = TimeBand.Morning,
            SeatsRequested = 1,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

        return code;
    }
}
