using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Notifications;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class GroupBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task RunOutboxDispatcherAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = ActivatorUtilities.CreateInstance<EmailOutboxDispatcher>(
            scope.ServiceProvider,
            Microsoft.Extensions.Options.Options.Create(new NotificationOptions
            {
                PickupDirectoryLocation = factory.PickupDirectory
            }));
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
    }

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
    public async Task Rejects_a_mixed_space_group_over_four_people_with_409()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace()
            with { AccessMode = SpaceAccessMode.Mixed, Capacity = 10 };
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails:
            [
                "uno@smartcampus.test", "due@smartcampus.test", "tre@smartcampus.test", "quattro@smartcampus.test"
            ]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Group too large", problem.GetProperty("title").GetString());
        Assert.Equal(4, problem.GetProperty("maxGroupSize").GetInt32());
    }

    [Fact]
    public async Task Accepts_a_mixed_space_group_of_four_people()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace()
            with { AccessMode = SpaceAccessMode.Mixed, Capacity = 10 };
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test", "due@smartcampus.test", "tre@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

        Assert.Equal(
            ["esterna@libero.test", "organizer@smartcampus.test", "registrata@smartcampus.test"],
            emails.Select(e => e.ToAddress));
        Assert.All(emails, e => Assert.Equal(OutboxEmailStatus.Pending, e.Status));

        await RunOutboxDispatcherAsync();

        using (var freshScope = factory.Services.CreateScope())
        {
            var freshContext = freshScope.ServiceProvider.GetRequiredService<BookingsDbContext>();
            var sent = await freshContext.OutboxEmails
                .AsNoTracking()
                .Where(e => e.BookingId == bookingId)
                .ToListAsync();
            Assert.All(sent, e => Assert.Equal(OutboxEmailStatus.Sent, e.Status));
        }

        // In the meantime the background service may have processed other queues from the same test suite:
        // count only the .eml files for this booking, identified by the reference code in file contents.
        var bookingReference = await dbContext.Bookings
            .Where(b => b.Id == bookingId)
            .Select(b => b.ReferenceCode)
            .SingleAsync();
        Assert.Equal(
            3,
            Directory.GetFiles(factory.PickupDirectory, "*.eml")
                .Count(f => File.ReadAllText(f).Contains(bookingReference)));
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

        // With 2 occupied seats 1 seat remains free: 2 new participants plus the organizer exceed capacity.
        var second = factory.CreateAuthenticatedClient(Guid.NewGuid(), "secondo@smartcampus.test");
        var response = await second.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["uno@smartcampus.test", "due@smartcampus.test"]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Not enough seats", problem.GetProperty("title").GetString());
    }
}
