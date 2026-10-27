using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Invitations;

public class MyInvitationsEndpointTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Start = new(2033, 4, 2, 9, 0, 0, DateTimeKind.Utc);

    private async Task CreateInvitationAsync(Guid? userId, string email, InvitationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
        var invitation = BookingTestData.NewInvitation(booking, userId, status);
        invitation.Email = email;
        booking.Invitations.Add(invitation);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task A_registered_user_finds_its_invitations()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        await CreateInvitationAsync(userId, email, InvitationStatus.Invited);
        await CreateInvitationAsync(userId, email, InvitationStatus.Accepted);

        var client = factory.CreateAuthenticatedClient(userId, email);
        var response = await client.GetAsync("/invitations/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitations = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, invitations.GetArrayLength());
    }
}
