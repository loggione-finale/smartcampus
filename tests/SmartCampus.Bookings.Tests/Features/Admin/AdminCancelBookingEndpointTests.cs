using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Admin;

public class AdminCancelBookingEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient AdminClient() =>
        factory.CreateAuthenticatedClient(Guid.NewGuid(), "admin@smartcampus.test", SmartCampusAuthConstants.RoleAdmin);

    private async Task<(Guid Id, string Code)> CreateGroupBookingAsync(string organizerEmail, params string[] participants)
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), organizerEmail);
        var start = TestSlots.Next();
        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(2), ParticipantEmails: participants));
        response.EnsureSuccessStatusCode();
        var booking = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return (booking.GetProperty("id").GetGuid(), booking.GetProperty("referenceCode").GetString()!);
    }

    [Fact]
    public async Task A_User_role_gets_403()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/admin/bookings/SC-X/cancel", new AdminCancelBookingRequest("Guasto"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_reason_returns_400_and_does_not_cancel(string reason)
    {
        var (id, code) = await CreateGroupBookingAsync("organizer-empty@smartcampus.test");

        var response = await AdminClient().PostAsJsonAsync($"/admin/bookings/{code}/cancel", new AdminCancelBookingRequest(reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, (await LoadBookingAsync(id)).Status);
    }

    [Fact]
    public async Task A_missing_reason_returns_400()
    {
        var (_, code) = await CreateGroupBookingAsync("organizer-nobody@smartcampus.test");

        var response = await AdminClient().PostAsJsonAsync($"/admin/bookings/{code}/cancel", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancels_the_booking_and_queues_an_email_for_every_participant()
    {
        var organizer = "organizer-all@smartcampus.test";
        var guests = new[] { "uno-all@smartcampus.test", "due-all@smartcampus.test" };
        var (id, code) = await CreateGroupBookingAsync(organizer, guests);

        var response = await AdminClient().PostAsJsonAsync($"/admin/bookings/{code}/cancel", new AdminCancelBookingRequest(" Aula inagibile "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booking = await LoadBookingAsync(id);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal("Aula inagibile", booking.CancellationReason);
        Assert.NotNull(booking.CancelledBy);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var emails = await db.OutboxEmails.Where(e => e.BookingId == id && e.Kind == "booking-cancelled").ToListAsync();
        Assert.Equal(3, emails.Count);
        Assert.Equal(
            new[] { organizer, guests[0], guests[1] }.Order(),
            emails.Select(e => e.ToAddress).Order());
        Assert.All(emails, e => Assert.Contains("Aula inagibile", e.Body));
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_code_and_409_when_already_cancelled()
    {
        var (_, code) = await CreateGroupBookingAsync("organizer-twice@smartcampus.test");
        var admin = AdminClient();
        var request = new AdminCancelBookingRequest("Guasto");

        var unknown = await admin.PostAsJsonAsync("/admin/bookings/SC-UNKNOWN/cancel", request);
        await admin.PostAsJsonAsync($"/admin/bookings/{code}/cancel", request);
        var twice = await admin.PostAsJsonAsync($"/admin/bookings/{code}/cancel", request);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
    }

    private async Task<Booking> LoadBookingAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        return await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == id);
    }
}
