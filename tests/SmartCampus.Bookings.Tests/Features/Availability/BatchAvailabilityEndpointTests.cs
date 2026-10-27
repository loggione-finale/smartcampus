using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Availability;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Availability;

public class BatchAvailabilityEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private Guid CreateSpace(SpaceAccessMode mode = SpaceAccessMode.Dedicated, int capacity = 30)
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
    public async Task Returns_400_when_request_body_is_missing()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/bookings/availability", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_400_when_space_ids_is_null()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);

        var payload = new
        {
            spaceIds = (Guid[]?)null,
            start = start,
            end = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("spaceIds", problem.Detail);
    }

    [Fact]
    public async Task Returns_400_when_more_than_200_space_ids_are_provided()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);
        var tooManyIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        var payload = new
        {
            spaceIds = tooManyIds,
            start = start,
            end = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("200", problem.Detail);
    }

    [Fact]
    public async Task Succeeds_when_exactly_200_space_ids_are_provided()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);
        var exactly200Ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();

        var payload = new
        {
            spaceIds = exactly200Ids,
            start = start,
            end = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(200, result.Count);
    }

    [Fact]
    public async Task Returns_400_when_start_or_end_is_missing()
    {
        var client = factory.CreateClient();
        var spaceId = Guid.NewGuid();

        var payload = new
        {
            spaceIds = new[] { spaceId },
            start = (DateTimeOffset?)null,
            end = (DateTimeOffset?)null
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_400_when_end_is_before_or_equal_to_start()
    {
        var client = factory.CreateClient();
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next(hour: 10);

        var payload = new
        {
            spaceIds = new[] { spaceId },
            start = start,
            end = start // same as start
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("strictly after", problem.Detail);
    }

    [Fact]
    public async Task Returns_400_when_times_are_not_utc()
    {
        var client = factory.CreateClient();
        var spaceId = Guid.NewGuid();
        var nonUtc = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.FromHours(2));

        var payload = new
        {
            spaceIds = new[] { spaceId },
            start = nonUtc,
            end = nonUtc.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("UTC", problem.Detail);
    }

    [Fact]
    public async Task Returns_empty_array_when_space_ids_is_empty()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);

        var payload = new
        {
            spaceIds = Array.Empty<Guid>(),
            start = start,
            end = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Deduplicates_space_ids_in_request()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);
        var spaceId = Guid.NewGuid();

        var payload = new
        {
            spaceIds = new[] { spaceId, spaceId, spaceId },
            start = start,
            end = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(spaceId, result[0]);
    }

    [Fact]
    public async Task Accepts_from_and_to_aliases_instead_of_start_and_end()
    {
        var client = factory.CreateClient();
        var start = TestSlots.Next(hour: 10);
        var spaceId = Guid.NewGuid();

        var payload = new
        {
            spaceIds = new[] { spaceId },
            from = start,
            to = start.AddHours(2)
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(spaceId, result[0]);
    }

    [Fact]
    public async Task Overlapping_space_is_excluded_while_adjacent_and_unbooked_spaces_are_included()
    {
        // 4 distinct spaces
        var spaceOverlapping = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var spaceAdjacentBefore = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var spaceAdjacentAfter = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var spaceUnbooked = CreateSpace(SpaceAccessMode.Dedicated, 30);

        // Requested slot: 10:00 - 12:00
        var slotStart = TestSlots.Next(hour: 10);
        var slotEnd = slotStart.AddHours(2);

        // User clients
        var user1 = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var user2 = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var user3 = factory.CreateAuthenticatedClient(Guid.NewGuid());

        // 1. spaceOverlapping has confirmed booking from 10:00 to 12:00 (overlaps requested range)
        await CreateBookingAsync(user1, spaceOverlapping, slotStart, slotEnd);

        // 2. spaceAdjacentBefore has confirmed booking from 08:00 to 10:00 (ends exactly when requested slot starts)
        await CreateBookingAsync(user2, spaceAdjacentBefore, slotStart.AddHours(-2), slotStart);

        // 3. spaceAdjacentAfter has confirmed booking from 12:00 to 14:00 (starts exactly when requested slot ends)
        await CreateBookingAsync(user3, spaceAdjacentAfter, slotEnd, slotEnd.AddHours(2));

        // 4. spaceUnbooked has no bookings

        var client = factory.CreateClient();
        var payload = new
        {
            spaceIds = new[] { spaceOverlapping, spaceAdjacentBefore, spaceAdjacentAfter, spaceUnbooked },
            start = slotStart,
            end = slotEnd
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availableIds = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(availableIds);

        // Assert: Overlapping space is NOT included
        Assert.DoesNotContain(spaceOverlapping, availableIds);

        // Assert: Adjacent spaces are INCLUDED
        Assert.Contains(spaceAdjacentBefore, availableIds);
        Assert.Contains(spaceAdjacentAfter, availableIds);

        // Assert: Unbooked space is INCLUDED
        Assert.Contains(spaceUnbooked, availableIds);

        Assert.Equal(3, availableIds.Count);
    }

    [Fact]
    public async Task Cancelled_bookings_do_not_block_availability()
    {
        var spaceCancelled = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var slotStart = TestSlots.Next(hour: 10);
        var slotEnd = slotStart.AddHours(2);

        var bookingId = await CreateBookingAsync(user, spaceCancelled, slotStart, slotEnd);

        // Cancel the booking
        var cancelResponse = await user.PostAsync($"/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Query availability for the cancelled slot
        var client = factory.CreateClient();
        var payload = new
        {
            spaceIds = new[] { spaceCancelled },
            start = slotStart,
            end = slotEnd
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availableIds = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(availableIds);
        Assert.Single(availableIds);
        Assert.Equal(spaceCancelled, availableIds[0]);
    }

    [Fact]
    public async Task Does_not_filter_out_spaces_with_bookings_on_different_days()
    {
        var spaceId = CreateSpace(SpaceAccessMode.Dedicated, 30);
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var day1Start = TestSlots.Next(hour: 10);
        // Booking on Day 1
        await CreateBookingAsync(user, spaceId, day1Start, day1Start.AddHours(2));

        // Query for Day 2 (Day 1 + 24h)
        var day2Start = day1Start.AddDays(1);
        var day2End = day2Start.AddHours(2);

        var client = factory.CreateClient();
        var payload = new
        {
            spaceIds = new[] { spaceId },
            start = day2Start,
            end = day2End
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availableIds = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(availableIds);
        Assert.Single(availableIds);
        Assert.Equal(spaceId, availableIds[0]);
    }

    [Fact]
    public async Task Mixed_space_with_free_seats_is_included_in_available_spaces()
    {
        var mixedSpace = CreateSpace(SpaceAccessMode.Mixed, 10);
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var slotStart = TestSlots.Next(hour: 10);
        var slotEnd = slotStart.AddHours(2);

        // 1 booking of 1 seat out of 10 capacity
        await CreateBookingAsync(user, mixedSpace, slotStart, slotEnd);

        var client = factory.CreateClient();
        var payload = new
        {
            spaceIds = new[] { mixedSpace },
            start = slotStart,
            end = slotEnd
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availableIds = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(availableIds);
        Assert.Single(availableIds);
        Assert.Equal(mixedSpace, availableIds[0]);
    }

    [Fact]
    public async Task Mixed_space_at_full_capacity_is_excluded_from_available_spaces()
    {
        var mixedSpace = CreateSpace(SpaceAccessMode.Mixed, 2);
        var user1 = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var user2 = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var slotStart = TestSlots.Next(hour: 10);
        var slotEnd = slotStart.AddHours(2);

        // 2 bookings of 1 seat each = 2/2 seats occupied
        await CreateBookingAsync(user1, mixedSpace, slotStart, slotEnd);
        await CreateBookingAsync(user2, mixedSpace, slotStart, slotEnd);

        var client = factory.CreateClient();
        var payload = new
        {
            spaceIds = new[] { mixedSpace },
            start = slotStart,
            end = slotEnd
        };

        var response = await client.PostAsJsonAsync("/bookings/availability", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availableIds = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions);
        Assert.NotNull(availableIds);
        Assert.Empty(availableIds);
    }
}
