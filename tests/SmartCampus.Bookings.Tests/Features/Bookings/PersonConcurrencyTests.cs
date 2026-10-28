using Microsoft.EntityFrameworkCore;

using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

/// <summary>
/// Test di concorrenza sulle persone (issue #62): molte prenotazioni simultanee, su spazi
/// diversi, che includono la stessa persona nella stessa fascia; ne passa esattamente una.
/// Gira sul database `bookings_test` reale (docker compose up, vedi infra/README.md).
/// Il vincolo che rende vero il test è `ex_invitations_person_no_overlap` sulla tabella
/// `invitations` (docs/decisions/0005-vincoli-di-esclusione.md).
/// </summary>
[Collection(nameof(BookingConcurrencyCollection))]
public class PersonConcurrencyTests
{
    private readonly BookingsTestDatabase _db;

    public PersonConcurrencyTests(BookingsTestDatabase db) => _db = db;

    [Fact]
    public async Task Concurrent_bookings_same_person_same_slot_only_one_succeeds()
    {
        const int attempts = 16;
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var tasks = Enumerable.Range(0, attempts)
            .Select(_ => TryInsertBookingWithParticipantAsync(Guid.NewGuid(), userId, start, end));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Success));

        await using var assertCtx = _db.CreateContext();
        var accepted = await assertCtx.Invitations
            .Where(i => i.UserId == userId && i.Status == InvitationStatus.Accepted)
            .CountAsync();
        Assert.Equal(1, accepted);
    }

    private async Task<BookingOutcome> TryInsertBookingWithParticipantAsync(
        Guid spaceId,
        Guid userId,
        DateTime start,
        DateTime end)
    {
        try
        {
            await using var ctx = _db.CreateContext();
            var booking = BookingTestData.NewBooking(spaceId, start, end);
            booking.Invitations.Add(BookingTestData.NewInvitation(booking, userId, InvitationStatus.Accepted));
            ctx.Bookings.Add(booking);
            await ctx.SaveChangesAsync();
            return new BookingOutcome(true, null);
        }
        catch (Exception ex)
        {
            return new BookingOutcome(false, ex);
        }
    }

    private sealed record BookingOutcome(bool Success, Exception? Error);
}
