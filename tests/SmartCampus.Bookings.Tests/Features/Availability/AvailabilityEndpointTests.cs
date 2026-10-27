using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Availability;

public class AvailabilityEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private Guid CreateSpace(SpaceAccessMode mode, int capacity)
    {
        var spaceId = Guid.NewGuid();
        var previous = factory.Spaces.Behavior;
        factory.Spaces.Behavior = id => id == spaceId
            ? FakeSpacesClient.DefaultSpace() with { AccessMode = mode, Capacity = capacity }
            : previous(id);
        return spaceId;
    }

    private async Task<Guid> CreateBookingAsync(HttpClient client, Guid spaceId, DateTimeOffset start, DateTimeOffset end)
    {
        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(spaceId, start, end));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Returns_400_when_date_query_parameter_is_missing()
    {
        var client = factory.CreateClient();
        var spaceId = Guid.NewGuid();

        var response = await client.GetAsync($"/bookings/availability/{spaceId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Missing date", problem!.Title);
    }

    [Fact]
    public async Task Returns_400_when_date_query_parameter_is_invalid()
    {
        var client = factory.CreateClient();
        var spaceId = Guid.NewGuid();

        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date=not-a-valid-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_when_space_does_not_exist()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? null : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date=2026-11-15");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Space not found", problem!.Title);
    }

    [Fact]
    public async Task Returns_503_when_spaces_service_is_unreachable()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = id => id == spaceId ? throw new SpacesUnavailableException("down") : FakeSpacesClient.DefaultSpace();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date=2026-11-15");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.Equal("Spaces unavailable", problem!.Title);
    }

    [Fact]
    public async Task Works_without_authentication_allow_anonymous()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var client = factory.CreateClient(); // Unauthenticated

        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date=2026-11-15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SpaceAvailabilityResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(spaceId, result.SpaceId);
    }

    [Fact]
    public async Task Returns_empty_slots_for_dedicated_space_without_bookings()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 25);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date=2026-11-20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SpaceAvailabilityResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(spaceId, result.SpaceId);
        Assert.Equal(new DateOnly(2026, 11, 20), result.Date);
        Assert.Equal(SpaceAccessMode.Dedicated, result.AccessMode);
        Assert.Equal(25, result.Capacity);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Returns_occupied_slots_for_dedicated_space_with_confirmed_booking()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next(hour: 10);
        var date = DateOnly.FromDateTime(start.UtcDateTime);

        await CreateBookingAsync(user, spaceId, start, start.AddHours(2));

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SpaceAvailabilityResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Slots);

        var slot = result.Slots[0];
        Assert.Equal(start.UtcDateTime, slot.Start.UtcDateTime);
        Assert.Equal(start.AddHours(2).UtcDateTime, slot.End.UtcDateTime);
        Assert.Equal(30, slot.OccupiedSeats);
        Assert.Equal(0, slot.FreeSeats);
        Assert.False(slot.IsAvailable);
    }

    [Fact]
    public async Task Cancelled_booking_does_not_occupy_dedicated_space()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next(hour: 10);
        var date = DateOnly.FromDateTime(start.UtcDateTime);

        var bookingId = await CreateBookingAsync(user, spaceId, start, start.AddHours(2));

        // Before cancel: 1 occupied slot
        var beforeCancel = await factory.CreateClient().GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);
        Assert.Single(beforeCancel!.Slots);

        // Cancel the booking
        var cancelResponse = await user.PostAsync($"/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // After cancel: slot is no longer occupied
        var afterCancel = await factory.CreateClient().GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);
        Assert.Empty(afterCancel!.Slots);
    }

    [Fact]
    public async Task Returns_occupied_seats_per_slot_for_mixed_space()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Mixed, 5);
        var start = TestSlots.Next(hour: 10);
        var date = DateOnly.FromDateTime(start.UtcDateTime);

        var user1 = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var user2 = factory.CreateAuthenticatedClient(Guid.NewGuid());

        // User 1 books 10:00 to 12:00 (1 seat)
        await CreateBookingAsync(user1, spaceId, start, start.AddHours(2));
        // User 2 books 11:00 to 13:00 (1 seat)
        await CreateBookingAsync(user2, spaceId, start.AddHours(1), start.AddHours(3));

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SpaceAvailabilityResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(3, result.Slots.Count);

        // Slot 1: 10:00 - 11:00 -> 1 seat occupied
        Assert.Equal(start.UtcDateTime, result.Slots[0].Start.UtcDateTime);
        Assert.Equal(start.AddHours(1).UtcDateTime, result.Slots[0].End.UtcDateTime);
        Assert.Equal(1, result.Slots[0].OccupiedSeats);
        Assert.Equal(4, result.Slots[0].FreeSeats);
        Assert.True(result.Slots[0].IsAvailable);

        // Slot 2: 11:00 - 12:00 -> 2 seats occupied
        Assert.Equal(start.AddHours(1).UtcDateTime, result.Slots[1].Start.UtcDateTime);
        Assert.Equal(start.AddHours(2).UtcDateTime, result.Slots[1].End.UtcDateTime);
        Assert.Equal(2, result.Slots[1].OccupiedSeats);
        Assert.Equal(3, result.Slots[1].FreeSeats);
        Assert.True(result.Slots[1].IsAvailable);

        // Slot 3: 12:00 - 13:00 -> 1 seat occupied
        Assert.Equal(start.AddHours(2).UtcDateTime, result.Slots[2].Start.UtcDateTime);
        Assert.Equal(start.AddHours(3).UtcDateTime, result.Slots[2].End.UtcDateTime);
        Assert.Equal(1, result.Slots[2].OccupiedSeats);
        Assert.Equal(4, result.Slots[2].FreeSeats);
        Assert.True(result.Slots[2].IsAvailable);
    }

    [Fact]
    public async Task Cancelled_booking_frees_seat_in_mixed_space()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Mixed, 2);
        var start = TestSlots.Next(hour: 14);
        var date = DateOnly.FromDateTime(start.UtcDateTime);

        var user1 = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var user2 = factory.CreateAuthenticatedClient(Guid.NewGuid());

        await CreateBookingAsync(user1, spaceId, start, start.AddHours(2));
        var booking2Id = await CreateBookingAsync(user2, spaceId, start, start.AddHours(2));

        // When both are confirmed: 2 seats occupied, 0 free
        var beforeCancel = await factory.CreateClient().GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);
        Assert.Single(beforeCancel!.Slots);
        Assert.Equal(2, beforeCancel.Slots[0].OccupiedSeats);
        Assert.Equal(0, beforeCancel.Slots[0].FreeSeats);
        Assert.False(beforeCancel.Slots[0].IsAvailable);

        // Cancel booking 2
        await user2.PostAsync($"/bookings/{booking2Id}/cancel", null);

        // After cancel: 1 seat occupied, 1 free, available
        var afterCancel = await factory.CreateClient().GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);
        Assert.Single(afterCancel!.Slots);
        Assert.Equal(1, afterCancel.Slots[0].OccupiedSeats);
        Assert.Equal(1, afterCancel.Slots[0].FreeSeats);
        Assert.True(afterCancel.Slots[0].IsAvailable);
    }

    [Fact]
    public async Task Pending_invitation_occupies_seat_in_mixed_space()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Mixed, 4);
        var start = TestSlots.Next(hour: 15);
        var end = start.AddHours(2);
        var date = DateOnly.FromDateTime(start.UtcDateTime);
        var period = new NpgsqlRange<DateTime>(start.UtcDateTime, true, end.UtcDateTime, false);

        // Seed a booking with an organizer and a pending invitation (Invited)
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var bookingId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                ReferenceCode = $"SC-AVAIL-{Guid.NewGuid():N}"[..16].ToUpper(),
                SpaceId = spaceId,
                SpaceNameSnapshot = "Sala Studio",
                SpaceTypeSnapshot = "StudyRoom",
                SpaceBuildingSnapshot = "Edificio A",
                SpaceAccessModeSnapshot = SpaceAccessMode.Mixed,
                SpaceCapacitySnapshot = 4,
                OrganizerId = Guid.NewGuid(),
                OrganizerNameSnapshot = "organizer@smartcampus.test",
                Period = period,
                SeatsRequested = 2,
                IsGroup = true,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Invitations =
                [
                    new Invitation
                    {
                        Id = Guid.NewGuid(),
                        BookingId = bookingId,
                        Email = "organizer@smartcampus.test",
                        Role = ParticipantRole.Organizer,
                        Status = InvitationStatus.Accepted,
                        Period = period,
                        ExpiresAt = start.UtcDateTime,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Invitation
                    {
                        Id = Guid.NewGuid(),
                        BookingId = bookingId,
                        Email = "participant@smartcampus.test",
                        Role = ParticipantRole.Participant,
                        Status = InvitationStatus.Invited, // Pending invitation!
                        Period = period,
                        ExpiresAt = start.UtcDateTime,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            };
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SpaceAvailabilityResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Slots);
        Assert.Equal(2, result.Slots[0].OccupiedSeats);
        Assert.Equal(2, result.Slots[0].FreeSeats);
        Assert.True(result.Slots[0].IsAvailable);
    }

    [Fact]
    public async Task Two_different_users_see_same_availability()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 20);
        var start = TestSlots.Next(hour: 9);
        var date = DateOnly.FromDateTime(start.UtcDateTime);

        var organizer = factory.CreateAuthenticatedClient(Guid.NewGuid());
        await CreateBookingAsync(organizer, spaceId, start, start.AddHours(3));

        var student1 = factory.CreateAuthenticatedClient(Guid.NewGuid(), "student1@smartcampus.test");
        var student2 = factory.CreateAuthenticatedClient(Guid.NewGuid(), "student2@smartcampus.test");

        var response1 = await student1.GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);
        var response2 = await student2.GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={date:yyyy-MM-dd}", JsonOptions);

        Assert.NotNull(response1);
        Assert.NotNull(response2);
        Assert.Equal(response1.Slots.Count, response2.Slots.Count);
        Assert.Equal(response1.Slots[0].Start, response2.Slots[0].Start);
        Assert.Equal(response1.Slots[0].End, response2.Slots[0].End);
        Assert.Equal(response1.Slots[0].OccupiedSeats, response2.Slots[0].OccupiedSeats);
    }

    [Fact]
    public async Task Clips_slots_spanning_across_day_boundaries()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var day1 = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(70), DateTimeKind.Utc);
        var day2 = day1.AddDays(1);

        // Booking spans across midnight: from 22:00 on Day 1 to 02:00 on Day 2
        var bookingStart = day1.AddHours(22);
        var bookingEnd = day2.AddHours(2);
        var period = new NpgsqlRange<DateTime>(bookingStart, true, bookingEnd, false);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            dbContext.Bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                ReferenceCode = $"SC-MIDNIGHT-{Guid.NewGuid():N}"[..16].ToUpper(),
                SpaceId = spaceId,
                SpaceNameSnapshot = "Aula 1",
                SpaceTypeSnapshot = "Classroom",
                SpaceBuildingSnapshot = "Edificio A",
                SpaceAccessModeSnapshot = SpaceAccessMode.Dedicated,
                SpaceCapacitySnapshot = 30,
                OrganizerId = Guid.NewGuid(),
                OrganizerNameSnapshot = "student@smartcampus.test",
                Period = period,
                SeatsRequested = 1,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        // Day 1: should be clipped from 22:00 to 00:00 (end of day 1)
        var res1 = await client.GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={DateOnly.FromDateTime(day1):yyyy-MM-dd}", JsonOptions);
        Assert.NotNull(res1);
        Assert.Single(res1.Slots);
        Assert.Equal(bookingStart, res1.Slots[0].Start.UtcDateTime);
        Assert.Equal(day2, res1.Slots[0].End.UtcDateTime);

        // Day 2: should be clipped from 00:00 (start of day 2) to 02:00
        var res2 = await client.GetFromJsonAsync<SpaceAvailabilityResponse>(
            $"/bookings/availability/{spaceId}?date={DateOnly.FromDateTime(day2):yyyy-MM-dd}", JsonOptions);
        Assert.NotNull(res2);
        Assert.Single(res2.Slots);
        Assert.Equal(day2, res2.Slots[0].Start.UtcDateTime);
        Assert.Equal(bookingEnd, res2.Slots[0].End.UtcDateTime);
    }
}
