using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class PendingClosureBlockingIntegrationTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<Booking> SeedBookingAsync(
        Guid organizerId,
        DateTime start,
        DateTime end,
        BookingStatus status = BookingStatus.Confirmed,
        int capacity = 30)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ReferenceCode = $"SC-T-{Guid.NewGuid():N}"[..24],
            SpaceId = Guid.NewGuid(),
            SpaceNameSnapshot = "Aula Seminari",
            SpaceTypeSnapshot = "Classroom",
            SpaceBuildingSnapshot = "Edificio B",
            SpaceAccessModeSnapshot = SpaceAccessMode.Dedicated,
            SpaceCapacitySnapshot = capacity,
            OrganizerId = organizerId,
            OrganizerNameSnapshot = "organizer@smartcampus.test",
            Period = new NpgsqlRange<DateTime>(start, true, end, false),
            BookingDate = DateOnly.FromDateTime(start),
            TimeBand = TimeBand.Morning,
            SeatsRequested = 1,
            Status = status,
            CreatedAt = start.AddHours(-1),
            UpdatedAt = start.AddHours(-1)
        };

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        return booking;
    }

    [Fact]
    public async Task Booking_creation_is_blocked_with_409_when_closing_comment_is_pending_and_succeeds_with_201_after_comment_submission()
    {
        // 1. Arrange: organizer has a past ended booking without comment
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var pastEnd = DateTime.UtcNow.AddHours(-2);
        var pastStart = pastEnd.AddHours(-2);
        var unclosedBooking = await SeedBookingAsync(organizerId, pastStart, pastEnd);

        // 2. Act: organizer attempts to create a new booking in the future
        var newSlotStart = TestSlots.Next();
        var createRequest1 = new CreateBookingRequest(Guid.NewGuid(), newSlotStart, newSlotStart.AddHours(2));
        var createResponse1 = await client.PostAsJsonAsync("/bookings", createRequest1);

        // Assert: 409 Conflict with the pending booking reference code
        Assert.Equal(HttpStatusCode.Conflict, createResponse1.StatusCode);
        var conflictDetails = await createResponse1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(conflictDetails.TryGetProperty("bookingToComment", out var bookingToCommentProp));
        Assert.Equal(unclosedBooking.ReferenceCode, bookingToCommentProp.GetString());

        // 3. Act: organizer submits closing comment for the finished booking
        var commentRequest = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 15,
            IssuesReported: false,
            Comment: "Aula utilizzata regolarmente, nessun problema.");

        var commentResponse = await client.PostAsJsonAsync(
            $"/bookings/{unclosedBooking.ReferenceCode}/closing-comment",
            commentRequest);

        // Assert: comment submission succeeds with 200 OK
        Assert.Equal(HttpStatusCode.OK, commentResponse.StatusCode);

        // Verify database state: booking is now Closed and ClosingComment is persisted
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var reloaded = await db.Bookings.Include(b => b.ClosingComment).SingleAsync(b => b.Id == unclosedBooking.Id);
            Assert.Equal(BookingStatus.Closed, reloaded.Status);
            Assert.NotNull(reloaded.ClosingComment);
            Assert.Equal(15, reloaded.ClosingComment.AttendeesCount);
        }

        // 4. Act: organizer attempts to create a new booking again
        var newSlotStart2 = TestSlots.Next();
        var createRequest2 = new CreateBookingRequest(Guid.NewGuid(), newSlotStart2, newSlotStart2.AddHours(2));
        var createResponse2 = await client.PostAsJsonAsync("/bookings", createRequest2);

        // Assert: creation succeeds with 201 Created!
        Assert.Equal(HttpStatusCode.Created, createResponse2.StatusCode);
        var createdBooking = await createResponse2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(organizerId, createdBooking.GetProperty("organizerId").GetGuid());
        Assert.Equal("Confirmed", createdBooking.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancelled_past_booking_does_not_block_new_booking_creation()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var pastEnd = DateTime.UtcNow.AddHours(-3);
        var pastStart = pastEnd.AddHours(-2);
        await SeedBookingAsync(organizerId, pastStart, pastEnd, status: BookingStatus.Cancelled);

        var slot = TestSlots.Next();
        var request = new CreateBookingRequest(Guid.NewGuid(), slot, slot.AddHours(2));
        var response = await client.PostAsJsonAsync("/bookings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Future_confirmed_booking_does_not_block_new_booking_creation()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var futureStart = DateTime.UtcNow.AddDays(2);
        var futureEnd = futureStart.AddHours(2);
        await SeedBookingAsync(organizerId, futureStart, futureEnd, status: BookingStatus.Confirmed);

        var slot = TestSlots.Next();
        var request = new CreateBookingRequest(Guid.NewGuid(), slot, slot.AddHours(2));
        var response = await client.PostAsJsonAsync("/bookings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Other_users_pending_closing_comment_does_not_block_different_user()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var pastEnd = DateTime.UtcNow.AddHours(-2);
        var pastStart = pastEnd.AddHours(-2);
        await SeedBookingAsync(userA, pastStart, pastEnd, status: BookingStatus.Confirmed);

        var clientB = factory.CreateAuthenticatedClient(userB);
        var slot = TestSlots.Next();
        var request = new CreateBookingRequest(Guid.NewGuid(), slot, slot.AddHours(2));
        var response = await clientB.PostAsJsonAsync("/bookings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Oldest_unclosed_booking_is_reported_when_multiple_past_uncommented_bookings_exist()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var olderEnd = DateTime.UtcNow.AddHours(-10);
        var olderStart = olderEnd.AddHours(-2);
        var olderBooking = await SeedBookingAsync(organizerId, olderStart, olderEnd);

        var newerEnd = DateTime.UtcNow.AddHours(-2);
        var newerStart = newerEnd.AddHours(-2);
        var newerBooking = await SeedBookingAsync(organizerId, newerStart, newerEnd);

        var slot = TestSlots.Next();
        var request = new CreateBookingRequest(Guid.NewGuid(), slot, slot.AddHours(2));
        var response = await client.PostAsJsonAsync("/bookings", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflictDetails = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(olderBooking.ReferenceCode, conflictDetails.GetProperty("bookingToComment").GetString());
    }
}
