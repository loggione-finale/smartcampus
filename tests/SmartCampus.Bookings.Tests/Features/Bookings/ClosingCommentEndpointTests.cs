using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class ClosingCommentEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<Booking> InsertBookingAsync(
        Guid organizerId,
        DateTime start,
        DateTime end,
        BookingStatus status = BookingStatus.Confirmed,
        int capacity = 30)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var booking = BookingTestData.NewBooking(
            spaceId: Guid.NewGuid(),
            start: start,
            end: end,
            status: status,
            organizerId: organizerId);
        booking.SpaceCapacitySnapshot = capacity;

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        return booking;
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var client = factory.CreateClient();
        var payload = new SubmitClosingCommentRequest(true, 10, false, "Aula usata regolarmente.");

        var response = await client.PostAsJsonAsync($"/bookings/SC-FAKE-0001/closing-comment", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Comment_before_end_returns_409()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        // Booking in the future (starts in 2 hours, ends in 4 hours)
        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(2), now.AddHours(4));

        var payload = new SubmitClosingCommentRequest(true, 10, false, "Aula usata regolarmente.");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Booking has not ended yet", problem.Title);
    }

    [Fact]
    public async Task Comment_by_another_user_returns_404()
    {
        var organizerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var strangerClient = factory.CreateAuthenticatedClient(strangerId, "stranger@smartcampus.test");

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-4), now.AddHours(-2));

        var payload = new SubmitClosingCommentRequest(true, 10, false, "Aula usata regolarmente.");

        var response = await strangerClient.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Booking not found", problem.Title);
    }

    [Fact]
    public async Task Comment_on_non_existent_code_returns_404()
    {
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var payload = new SubmitClosingCommentRequest(true, 10, false, "Aula usata regolarmente.");

        var response = await client.PostAsJsonAsync("/bookings/NON-EXISTENT-CODE/closing-comment", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_invalid_fields_with_400()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-4), now.AddHours(-2), capacity: 20);

        // Comment too short (< 10 chars) and attendees exceed capacity (25 > 20)
        var payload = new SubmitClosingCommentRequest(true, 25, false, "Short");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Comment"));
        Assert.True(problem.Errors.ContainsKey("AttendeesCount"));
    }

    [Fact]
    public async Task Submits_closing_comment_successfully_by_reference_code()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-3), now.AddHours(-1), capacity: 30);

        var payload = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 15,
            IssuesReported: false,
            Comment: "Tutto perfetto, aula pulita e ordinata.");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ClosingCommentResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.BookingId);
        Assert.Equal(booking.ReferenceCode, result.ReferenceCode);
        Assert.Equal(organizerId, result.AuthorId);
        Assert.True(result.SpaceUsed);
        Assert.Equal(15, result.AttendeesCount);
        Assert.False(result.IssuesReported);
        Assert.Equal("Tutto perfetto, aula pulita e ordinata.", result.Comment);
        Assert.Equal(BookingStatus.Closed, result.BookingStatus);

        // Verify database state
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var updated = await dbContext.Bookings.Include(b => b.ClosingComment).FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Closed, updated.Status);
        Assert.NotNull(updated.ClosingComment);
        Assert.Equal("Tutto perfetto, aula pulita e ordinata.", updated.ClosingComment.Comment);
        Assert.Equal(15, updated.ClosingComment.AttendeesCount);
    }

    [Fact]
    public async Task Submits_closing_comment_successfully_by_guid_id()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-3), now.AddHours(-1), capacity: 30);

        var payload = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 8,
            IssuesReported: false,
            Comment: "Uso regolare dell'aula studio.");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.Id}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ClosingCommentResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.BookingId);
        Assert.Equal(BookingStatus.Closed, result.BookingStatus);
    }

    [Fact]
    public async Task Submitting_with_issues_reported_creates_admin_alert()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-3), now.AddHours(-1), capacity: 20);

        var payload = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 12,
            IssuesReported: true,
            Comment: "Proiettore guasto e cavo HDMI mancante.");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify admin alert in database
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var alert = await dbContext.AdminAlerts.FirstOrDefaultAsync(a => a.BookingId == booking.Id);
        Assert.NotNull(alert);
        Assert.Equal(AdminAlertKind.ReportedIssue, alert.Kind);
        Assert.Equal(AdminAlertStatus.Open, alert.Status);
        Assert.Contains(booking.ReferenceCode, alert.Message);
        Assert.Contains("Proiettore guasto", alert.Message);
    }

    [Fact]
    public async Task Submitting_twice_returns_409()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-3), now.AddHours(-1), capacity: 30);

        var payload = new SubmitClosingCommentRequest(true, 10, false, "Primo commento regolare.");
        var response1 = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var response2 = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        var problem = await response2.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Closing comment already submitted", problem.Title);
    }

    [Fact]
    public async Task Submitting_on_cancelled_booking_returns_409()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(
            organizerId, now.AddHours(-3), now.AddHours(-1), status: BookingStatus.Cancelled);

        var payload = new SubmitClosingCommentRequest(false, 0, false, "Prenotazione che era stata annullata.");

        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Booking already cancelled", problem.Title);
    }

    [Fact]
    public async Task Unblocks_pending_closure_check_after_comment_is_submitted()
    {
        var organizerId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(organizerId);

        var now = DateTime.UtcNow;
        var booking = await InsertBookingAsync(organizerId, now.AddHours(-3), now.AddHours(-1), capacity: 30);

        using (var scope = factory.Services.CreateScope())
        {
            var checker = scope.ServiceProvider.GetRequiredService<IPendingClosureChecker>();
            var pendingCode = await checker.GetPendingReferenceCodeAsync(organizerId, default);
            Assert.Equal(booking.ReferenceCode, pendingCode);
        }

        var payload = new SubmitClosingCommentRequest(true, 10, false, "Commento per sbloccare le nuove prenotazioni.");
        var response = await client.PostAsJsonAsync($"/bookings/{booking.ReferenceCode}/closing-comment", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var checker = scope.ServiceProvider.GetRequiredService<IPendingClosureChecker>();
            var pendingCode = await checker.GetPendingReferenceCodeAsync(organizerId, default);
            Assert.Null(pendingCode);
        }
    }
}
