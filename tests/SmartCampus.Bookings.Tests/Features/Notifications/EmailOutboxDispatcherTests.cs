using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Notifications;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Notifications;

/// <summary>
/// La coda di outbox non deve mai far fallire l'operazione HTTP già salvata:
/// i .eml li scrive il dispatcher in background, con esito tracciato sulla riga.
/// </summary>
public class EmailOutboxDispatcherTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<(Guid BookingId, string ReferenceCode)> CreateBookingAsync(HttpClient client)
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceCode").GetString()!);
    }

    [Fact]
    public async Task Creating_a_booking_with_an_unwritable_pickup_directory_still_returns_201_and_keeps_the_rows_pending()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");

        // Percorso non scrivibile: su Windows un file esistente al posto della cartella.
        var blockedDirectory = Path.Combine(
            Path.GetTempPath(), "smartcampus-tests-blocked", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(blockedDirectory)!);
        await File.WriteAllTextAsync(blockedDirectory, "not a directory");

        try
        {
            var (bookingId, _) = await CreateBookingAsync(client);

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

            var pending = await dbContext.OutboxEmails
                .Where(e => e.BookingId == bookingId)
                .ToListAsync();
            var bookingCreated = Assert.Single(pending, e => e.Kind == "booking-created");
            Assert.Equal(OutboxEmailStatus.Pending, bookingCreated.Status);

            var dispatcher = ActivatorUtilities.CreateInstance<EmailOutboxDispatcher>(
                scope.ServiceProvider,
                Options.Create(new NotificationOptions { PickupDirectoryLocation = blockedDirectory }));
            await dispatcher.DispatchPendingAsync(CancellationToken.None);

            var failed = await dbContext.OutboxEmails
                .AsNoTracking()
                .SingleAsync(e => e.BookingId == bookingId && e.Kind == "booking-created");
            Assert.Equal(OutboxEmailStatus.Failed, failed.Status);
            Assert.NotEmpty(failed.LastError!);
            Assert.Equal(1, failed.Attempts);
        }
        finally
        {
            File.Delete(blockedDirectory);
        }
    }

    [Fact]
    public async Task DispatchPendingAsync_writes_the_eml_and_marks_the_row_sent()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), "organizer@smartcampus.test");
        var (bookingId, referenceCode) = await CreateBookingAsync(client);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var dispatcher = ActivatorUtilities.CreateInstance<EmailOutboxDispatcher>(
            scope.ServiceProvider,
            Options.Create(new NotificationOptions { PickupDirectoryLocation = factory.PickupDirectory }));
        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        var sent = await dbContext.OutboxEmails
            .AsNoTracking()
            .SingleAsync(e => e.BookingId == bookingId && e.Kind == "booking-created");
        Assert.Equal(OutboxEmailStatus.Sent, sent.Status);
        Assert.NotNull(sent.SentAt);

        var eml = Directory
            .GetFiles(factory.PickupDirectory, "*.eml")
            .Single(f => File.ReadAllText(f).Contains(referenceCode));
        Assert.Contains(sent.Subject, File.ReadAllText(eml));

        // Un secondo colpo di dispatch non riscrive nulla: la riga è già Sent.
        await dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Single(
            Directory.GetFiles(factory.PickupDirectory, "*.eml"),
            f => File.ReadAllText(f).Contains(referenceCode));
    }

    [Fact]
    public async Task The_booking_created_email_goes_to_the_organizer_invitation_email_not_the_display_name()
    {
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        // Il token ha ClaimTypes.Name = nome visualizzato ("Giulia Rossi"), non un indirizzo:
        // prima della correzione il destinatario era quel nome e MailAddress sollevava FormatException.
        var client = factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "organizer@smartcampus.test", displayName: "Giulia Rossi");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bookingId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var bookingCreated = await dbContext.OutboxEmails
            .AsNoTracking()
            .SingleAsync(e => e.BookingId == bookingId && e.Kind == "booking-created");
        Assert.Equal("organizer@smartcampus.test", bookingCreated.ToAddress);
        Assert.DoesNotContain("Giulia Rossi", bookingCreated.ToAddress);
        Assert.Equal(OutboxEmailStatus.Pending, bookingCreated.Status);
    }

    [Fact]
    public async Task The_booking_cancelled_email_goes_to_the_organizer_invitation_email_not_the_display_name()
    {
        var organizerId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        factory.Spaces.Behavior = _ => FakeSpacesClient.DefaultSpace();
        var client = factory.CreateAuthenticatedClient(
            organizerId, "organizer@smartcampus.test", displayName: "Giulia Rossi");
        var start = TestSlots.Next();

        var created = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            spaceId, start, start.AddHours(1)));
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
            .ToListAsync();
        var organizerEmail = Assert.Single(cancellationEmails);
        Assert.Equal("organizer@smartcampus.test", organizerEmail.ToAddress);
        Assert.DoesNotContain("Giulia Rossi", organizerEmail.ToAddress);
        Assert.Equal(OutboxEmailStatus.Pending, organizerEmail.Status);
    }
}
