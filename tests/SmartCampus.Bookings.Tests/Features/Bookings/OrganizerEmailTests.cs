using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class OrganizerEmailTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient ClientWithName(Guid userId, string email, string? displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, SmartCampusAuthConstants.RoleUser)
        };
        if (email.Length > 0)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (displayName is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, displayName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(BookingsApiFactory.SigningKey));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    [Fact]
    public async Task The_organizer_invitation_has_the_email_and_the_display_name_is_kept_apart()
    {
        var userId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var client = ClientWithName(userId, email, "Giulia Rossi");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bookingId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Invitations).SingleAsync(b => b.Id == bookingId);
        var organizerInvitation = Assert.Single(booking.Invitations, i => i.Role == ParticipantRole.Organizer);
        Assert.Equal(email, organizerInvitation.Email);
        Assert.Equal("Giulia Rossi", organizerInvitation.DisplayNameSnapshot);
        Assert.Equal("Giulia Rossi", booking.OrganizerNameSnapshot);
    }

    [Fact]
    public async Task Without_a_display_name_the_email_is_used_as_the_name()
    {
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var client = ClientWithName(Guid.NewGuid(), email, null);
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var bookingId = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId);
        Assert.Equal(email, booking.OrganizerNameSnapshot);
    }

    [Fact]
    public async Task A_token_without_an_email_is_rejected()
    {
        var client = ClientWithName(Guid.NewGuid(), string.Empty, "Giulia Rossi");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(Guid.NewGuid(), start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_organizer_email_cannot_be_invited_as_a_participant()
    {
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        var client = ClientWithName(Guid.NewGuid(), email, "Giulia Rossi");
        var start = TestSlots.Next();

        var response = await client.PostAsJsonAsync("/bookings", new CreateBookingRequest(
            Guid.NewGuid(), start, start.AddHours(2), new[] { email }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
