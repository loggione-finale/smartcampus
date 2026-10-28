using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class GroupBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Rejects_a_group_beyond_the_space_capacity_with_409()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace() with { Capacity = 2 };
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test", "due@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Not enough seats", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Rejects_duplicate_emails_and_the_organizer_email_with_400()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var duplicates = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test", "DUE@smartcampus.test", "due@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.BadRequest, duplicates.StatusCode);

        var organizer = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1),
            ParticipantEmails: ["organizer@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.BadRequest, organizer.StatusCode);
    }

    [Fact]
    public async Task Returns_503_when_Users_is_unreachable()
    {
        factory.Users.IsDown = true;
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test"]));

        factory.Users.IsDown = false;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Users unavailable", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Creates_the_invitations_with_the_registered_users_and_enqueues_the_emails()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        factory.Users.RegisteredUsers =
        [
            new(Guid.NewGuid(), "registrata@smartcampus.test", "Registrata Uno")
        ];
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId, "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["Registrata@smartcampus.test", "esterna@libero.test"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bookingId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var invitations = await dbContext.Invitations
            .Where(i => i.BookingId == bookingId && i.Role == ParticipantRole.Participant)
            .OrderBy(i => i.Email)
            .ToListAsync();

        Assert.Equal(2, invitations.Count);

        var registered = invitations.Single(i => i.Email == "registrata@smartcampus.test");
        Assert.Equal(InvitationStatus.Invited, registered.Status);
        Assert.NotNull(registered.UserId);
        Assert.Equal("Registrata Uno", registered.DisplayNameSnapshot);

        var external = invitations.Single(i => i.Email == "esterna@libero.test");
        Assert.Null(external.UserId);
        Assert.Null(external.DisplayNameSnapshot);

        var emails = await dbContext.OutboxEmails
            .Where(e => e.BookingId == bookingId)
            .OrderBy(e => e.ToAddress)
            .ToListAsync();

        Assert.Equal(["esterna@libero.test", "registrata@smartcampus.test"], emails.Select(e => e.ToAddress));
        Assert.All(emails, e => Assert.Equal(OutboxEmailStatus.Pending, e.Status));
    }

    [Fact]
    public async Task Counts_occupied_seats_of_pending_invitations_in_mixed_spaces()
    {
        var spaceId = Guid.NewGuid();
        var start = TestSlots.Next();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace() with
        {
            Capacity = 3,
            AccessMode = SpaceAccessMode.Mixed
        };

        // Organizzatore + 1 invitato pendente = 2 posti occupati.
        var first = factory.CreateAuthenticatedClient(Guid.NewGuid(), "primo@smartcampus.test");
        var created = await first.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["invitato@smartcampus.test"]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Con 2 posti occupati restano 1 posto libero: 2 nuovi partecipanti oltre l'organizzatore superano la capienza.
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid(), "secondo@smartcampus.test");
        var response = await second.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test", "due@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Not enough seats", problem.GetProperty("title").GetString());
    }
}
