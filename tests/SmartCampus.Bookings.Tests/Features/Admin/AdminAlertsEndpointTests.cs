using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Admin;

public class AdminAlertsEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient AdminClient() =>
        factory.CreateAuthenticatedClient(Guid.NewGuid(), "admin@smartcampus.test", SmartCampusAuthConstants.RoleAdmin);

    private async Task<Guid> AddAlertAsync(AdminAlertStatus status = AdminAlertStatus.Open)
    {
        var alert = new AdminAlert
        {
            Id = Guid.NewGuid(),
            Kind = AdminAlertKind.Maintenance,
            Message = "Proiettore guasto",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        db.AdminAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    [Fact]
    public async Task A_User_role_gets_403_on_both_endpoints()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var list = await client.GetAsync("/admin/alerts");
        var resolve = await client.PostAsync($"/admin/alerts/{Guid.NewGuid()}/resolve", null);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, resolve.StatusCode);
    }

    [Fact]
    public async Task Every_new_booking_creates_a_NewBooking_alert()
    {
        var user = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var start = TestSlots.Next();
        var created = await user.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));
        created.EnsureSuccessStatusCode();
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var alerts = await AdminClient().GetFromJsonAsync<JsonElement>("/admin/alerts?status=Open", JsonOptions);

        var alert = Assert.Single(
            alerts.EnumerateArray(),
            a => a.TryGetProperty("bookingId", out var id) && id.ValueKind == JsonValueKind.String && id.GetGuid() == bookingId);
        Assert.Equal("NewBooking", alert.GetProperty("kind").GetString());
        Assert.Equal("Open", alert.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Resolving_marks_the_alert_as_handled_and_removes_it_from_the_open_list()
    {
        var alertId = await AddAlertAsync();
        var admin = AdminClient();

        var response = await admin.PostAsJsonAsync($"/admin/alerts/{alertId}/resolve", new ResolveAdminAlertRequest("Sistemato"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resolved = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Handled", resolved.GetProperty("status").GetString());
        Assert.Equal("Sistemato", resolved.GetProperty("handlingNote").GetString());

        var open = await admin.GetFromJsonAsync<JsonElement>("/admin/alerts?status=Open", JsonOptions);
        Assert.DoesNotContain(open.EnumerateArray(), a => a.GetProperty("id").GetGuid() == alertId);
    }

    [Fact]
    public async Task Resolving_twice_returns_409_and_an_unknown_alert_404()
    {
        var alertId = await AddAlertAsync(AdminAlertStatus.Handled);
        var admin = AdminClient();

        var twice = await admin.PostAsync($"/admin/alerts/{alertId}/resolve", null);
        var unknown = await admin.PostAsync($"/admin/alerts/{Guid.NewGuid()}/resolve", null);

        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
