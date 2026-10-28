using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Notifications;

public class OutboxEmailE2ETests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Creating_a_booking_enqueues_the_booking_created_email_and_the_invitation_emails()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        factory.Users.RegisteredUsers =
        [
            new(Guid.NewGuid(), "registrata@smartcampus.test", "Registrata Uno")
        ];
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["Registrata@smartcampus.test", "esterna@libero.test"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bookingId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var emails = await dbContext.OutboxEmails
            .Where(e => e.BookingId == bookingId)
            .OrderBy(e => e.Kind)
            .ThenBy(e => e.ToAddress)
            .ToListAsync();

        // L'invito registrato ha UserId, quello non registrato no:
        // il componente BookingService prenota le mail solo per gli inviti con status Invited,
        // entrambi i partecipanti lo sono, quindi si attendono due inviti + la prenotazione creata.
        Assert.Equal(
            ["booking-created", "invitation", "invitation"],
            emails.Select(e => e.Kind));
        Assert.Equal(
            ["organizer@smartcampus.test", "esterna@libero.test", "registrata@smartcampus.test"],
            emails.Select(e => e.ToAddress));
        Assert.All(emails, e => Assert.Equal(OutboxEmailStatus.Sent, e.Status));

        // Ogni evento produce la mail attesa: invito separato per registrato e non registrato.
        var invitationToExternal = emails.Single(e => e.ToAddress == "esterna@libero.test");
        Assert.Contains("Registrati sul portale", invitationToExternal.Body);
        var invitationToRegistered = emails.Single(e => e.ToAddress == "registrata@smartcampus.test");
        Assert.Contains("utente registrato", invitationToRegistered.Body);
    }

    [Fact]
    public async Task Cancelling_a_booking_enqueues_the_booking_cancelled_emails()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId, "organizer@smartcampus.test");
        var start = TestSlots.Next();

        var created = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1),
            ParticipantEmails: ["invitato@smartcampus.test"]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        var cancelled = await client.PostAsync($"/bookings/{bookingId}/cancel", null);
        Assert.True(
            cancelled.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Unexpected status {cancelled.StatusCode}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var cancellationEmails = await dbContext.OutboxEmails
            .Where(e => e.BookingId == bookingId && e.Kind == "booking-cancelled")
            .OrderBy(e => e.ToAddress)
            .ToListAsync();

        Assert.Equal(
            ["invitato@smartcampus.test", "organizer@smartcampus.test"],
            cancellationEmails.Select(e => e.ToAddress));
    }
}
