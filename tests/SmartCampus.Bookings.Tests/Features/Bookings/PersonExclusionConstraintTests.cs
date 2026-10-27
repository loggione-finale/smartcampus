using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class PersonExclusionConstraintTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Start = new(2032, 6, 14, 9, 0, 0, DateTimeKind.Utc);

    private async Task InsertParticipantAsync(Guid userId, DateTime start, DateTime end, InvitationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = BookingTestData.NewBooking(Guid.NewGuid(), start, end);
        booking.Invitations.Add(BookingTestData.NewInvitation(booking, userId, status));
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task A_person_cannot_be_accepted_in_two_overlapping_bookings()
    {
        var userId = Guid.NewGuid();
        await InsertParticipantAsync(userId, Start, Start.AddHours(2), InvitationStatus.Accepted);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertParticipantAsync(userId, Start.AddHours(1), Start.AddHours(3), InvitationStatus.Accepted));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("ex_invitations_person_no_overlap", postgres.ConstraintName);
    }

    [Theory]
    [InlineData(InvitationStatus.Invited)]
    [InlineData(InvitationStatus.Expired)]
    [InlineData(InvitationStatus.Left)]
    [InlineData(InvitationStatus.Cancelled)]
    public async Task Only_accepted_invitations_count_for_the_person_constraint(InvitationStatus status)
    {
        var userId = Guid.NewGuid();
        await InsertParticipantAsync(userId, Start, Start.AddHours(2), InvitationStatus.Accepted);

        await InsertParticipantAsync(userId, Start.AddHours(1), Start.AddHours(3), status);
    }

    [Fact]
    public async Task An_accepted_invitation_is_allowed_next_to_a_pending_one()
    {
        var userId = Guid.NewGuid();
        await InsertParticipantAsync(userId, Start, Start.AddHours(2), InvitationStatus.Invited);

        await InsertParticipantAsync(userId, Start, Start.AddHours(2), InvitationStatus.Accepted);
    }

    [Fact]
    public async Task Accepted_invitations_in_adjacent_bookings_are_allowed()
    {
        var userId = Guid.NewGuid();
        await InsertParticipantAsync(userId, Start, Start.AddHours(2), InvitationStatus.Accepted);

        await InsertParticipantAsync(userId, Start.AddHours(2), Start.AddHours(4), InvitationStatus.Accepted);
    }

    [Fact]
    public async Task Invitations_of_unregistered_people_never_conflict()
    {
        var email = $"{Guid.NewGuid():N}@smartcampus.test";
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var booking = BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2));
            var invitation = BookingTestData.NewInvitation(booking, null, InvitationStatus.Accepted);
            invitation.Email = email;
            booking.Invitations.Add(invitation);
            dbContext.Bookings.Add(booking);
        }

        await dbContext.SaveChangesAsync();
    }
}
