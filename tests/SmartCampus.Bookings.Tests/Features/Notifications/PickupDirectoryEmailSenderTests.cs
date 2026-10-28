using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Notifications;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Notifications;

public class PickupDirectoryEmailSenderTests : IDisposable
{
    private static readonly DateTime Start = new(2033, 3, 30, 9, 0, 0, DateTimeKind.Utc);

    private readonly string pickupDirectory = Path.Combine(
        Path.GetTempPath(), "smartcampus-tests", Guid.NewGuid().ToString("N"));

    private readonly PickupDirectoryEmailSender sender;

    public PickupDirectoryEmailSenderTests() =>
        sender = new PickupDirectoryEmailSender(Options.Create(new NotificationOptions
        {
            PickupDirectoryLocation = pickupDirectory
        }));

    public void Dispose()
    {
        if (Directory.Exists(pickupDirectory))
        {
            Directory.Delete(pickupDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Writes_the_expected_eml_file_for_each_event()
    {
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invitation = BookingTestData.NewInvitation(booking, userId: null, InvitationStatus.Invited);

        var emails = new[]
        {
            NotificationTemplates.BookingCreated(booking, "organizer@smartcampus.test"),
            NotificationTemplates.InvitationToRegistered(invitation, booking),
            NotificationTemplates.InvitationToUnregistered(invitation, booking),
            NotificationTemplates.InvitationExpired(invitation, booking, invitation.Email, toOrganizer: false),
            NotificationTemplates.InvitationExpired(invitation, booking, "organizer@smartcampus.test", toOrganizer: true),
            NotificationTemplates.BookingCancelled(booking, "organizer@smartcampus.test")
        };

        foreach (var email in emails)
        {
            await sender.SendAsync(email);
        }

        var files = Directory.GetFiles(pickupDirectory, "*.eml");
        Assert.Equal(emails.Length, files.Length);

        var subjects = files
            .Select(file => File.ReadLines(file)
                .First(l => l.StartsWith("Subject:", StringComparison.Ordinal))["Subject:".Length..].Trim())
            .OrderBy(s => s)
            .ToArray();
        Assert.Equal(
            emails.Select(e => e.Subject).OrderBy(s => s),
            subjects);

        foreach (var file in files)
        {
            var lines = File.ReadLines(file).ToArray();

            // Il corpo con UTF-8 è base64: si decodifica prima del confronto.
            var body = lines.Contains("Content-Transfer-Encoding: base64")
                ? Encoding.UTF8.GetString(Convert.FromBase64String(
                    string.Concat(lines.SkipWhile(l => l.Length > 0).Skip(1))))
                : File.ReadAllText(file);

            var sent = Assert.Single(emails, e =>
                e.ToAddress == lines.First(l => l.StartsWith("To:", StringComparison.Ordinal))["To:".Length..].Trim()
                && body.Replace(" ", string.Empty).Contains(e.Body.Replace(" ", string.Empty)));
            Assert.Equal(sent.Subject, lines.First(l => l.StartsWith("Subject:", StringComparison.Ordinal))["Subject:".Length..].Trim());
        }
    }

    [Fact]
    public async Task Never_talks_to_an_smtp_server()
    {
        // DeliveryMethod è pickup directory: l'invio non apre connessioni di rete.
        var email = NotificationTemplates.BookingCreated(
            BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(1)),
            "organizer@smartcampus.test");

        await sender.SendAsync(email);

        Assert.Single(Directory.GetFiles(pickupDirectory, "*.eml"));
    }

    [Fact]
    public void Templates_format_times_in_the_campus_timezone_not_utc()
    {
        // 09:00 UTC = 11:00 ora italiana (CEST, UTC+2 a fine marzo).
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invitation = BookingTestData.NewInvitation(booking, userId: null, InvitationStatus.Invited);

        var email = NotificationTemplates.BookingCreated(booking, "organizer@smartcampus.test");

        Assert.Contains("30/03/2033 11:00 - 13:00", email.Body);
        Assert.DoesNotContain("09:00", email.Body);

        var invitationEmail = NotificationTemplates.InvitationToUnregistered(invitation, booking);
        // ExpiresAt coincide con l'inizio della prenotazione: 09:00 UTC = 11:00.
        Assert.Contains("conferma entro 11:00", invitationEmail.Body);
    }

    [Fact]
    public void Templates_use_one_kind_per_event()
    {
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var registered = BookingTestData.NewInvitation(booking, Guid.NewGuid(), InvitationStatus.Invited);
        var unregistered = BookingTestData.NewInvitation(booking, userId: null, InvitationStatus.Invited);

        Assert.Equal("booking-created", NotificationTemplates.BookingCreated(booking, "o@smartcampus.test").Kind);
        Assert.Equal("invitation", NotificationTemplates.InvitationToRegistered(registered, booking).Kind);
        Assert.Equal("invitation", NotificationTemplates.InvitationToUnregistered(unregistered, booking).Kind);
        Assert.Equal(
            "invitation-expired",
            NotificationTemplates.InvitationExpired(registered, booking, registered.Email, toOrganizer: false).Kind);
        Assert.Equal("booking-cancelled", NotificationTemplates.BookingCancelled(booking, "o@smartcampus.test").Kind);
    }
}
