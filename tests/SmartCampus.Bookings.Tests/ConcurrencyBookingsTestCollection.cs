using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Tests;

/// <summary>
/// Test di integrazione sulla concorrenza (issue #35): molte richieste simultanee sullo stesso
/// spazio devono rispettare il vincolo di non sovrapposizione (aula dedicata) e la capienza
/// (aula mista); due prenotazioni adiacenti sono entrambe accettate.
/// Gira sul database `bookings_test` reale (docker compose up, vedi infra/README.md).
/// La verifica negativa (test che fallisce senza vincolo o senza lock) è una prova manuale:
/// la procedura è in docs/evidence/booking-concurrency.md, task #36.
/// </summary>
public sealed class BookingsTestDatabase : IAsyncLifetime
{
    // La password reale arriva dalla variabile d'ambiente BOOKINGS_TEST_CONNECTION_STRING
    // (vedi infra/README.md); questa stringa è il fallback di sviluppo locale del gruppo.
    internal static readonly string ConnectionString = Environment.GetEnvironmentVariable("BOOKINGS_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=bookings_test;Username=bookings_app;Password=bookings";

    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        // `bookings_app` non ha i diritti per ricreare il database: ripulisce solo le tabelle.
        // I nomi delle tabelle sono fissi (niente input utente), il warning di SQL injection non si applica.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        foreach (var table in new[] { "outbox_emails", "admin_alerts", "closing_comments", "equipment_requests", "invitations", "bookings" })
        {
#pragma warning disable EF1002 // Nomi di tabella fissi, nessun input utente
            await ctx.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {table} CASCADE");
#pragma warning restore EF1002
        }
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }

    internal BookingsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(_dataSource)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new BookingsDbContext(options);
    }
}

[CollectionDefinition(nameof(BookingConcurrencyCollection))]
public class BookingConcurrencyCollection : ICollectionFixture<BookingsTestDatabase>;

[Collection(nameof(BookingConcurrencyCollection))]
public class BookingConcurrencyTests
{
    private readonly BookingsTestDatabase _db;

    public BookingConcurrencyTests(BookingsTestDatabase db) => _db = db;

    [Fact]
    public async Task Concurrent_bookings_same_dedicated_space_only_one_succeeds()
    {
        const int attempts = 16;
        var spaceId = Guid.NewGuid();
        var start = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var tasks = Enumerable.Range(0, attempts).Select(_ => TryInsertBookingAsync(spaceId, start, end));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Success));

        await using var assertCtx = _db.CreateContext();
        var confirmed = await assertCtx.Bookings
            .Where(b => b.SpaceId == spaceId && b.Status == BookingStatus.Confirmed)
            .CountAsync();
        Assert.Equal(1, confirmed);
    }

    [Fact]
    public async Task Concurrent_bookings_mixed_space_never_oversell_with_lock()
    {
        const int capacity = 10;
        const int attempts = 16;
        const int seatsPerBooking = 2;
        var spaceId = Guid.NewGuid();
        var start = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var tasks = Enumerable.Range(0, attempts)
            .Select(_ => TryInsertMixedBookingWithLockAsync(spaceId, start, end, capacity, seatsPerBooking));
        var results = await Task.WhenAll(tasks);

        var accepted = results.Count(r => r.Success);
        Assert.True(accepted * seatsPerBooking <= capacity,
            $"Accettate {accepted} prenotazioni da {seatsPerBooking} posti: superata la capienza {capacity}.");
    }

    [Fact]
    public async Task Adjacent_bookings_both_accepted()
    {
        var spaceId = Guid.NewGuid();
        var first = new NpgsqlRange<DateTime>(
            new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc), true,
            new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc), false);
        var second = new NpgsqlRange<DateTime>(
            new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc), true,
            new DateTime(2026, 11, 3, 11, 0, 0, DateTimeKind.Utc), false);

        var r1 = await TryInsertBookingAsync(spaceId, first.LowerBound, first.UpperBound);
        var r2 = await TryInsertBookingAsync(spaceId, second.LowerBound, second.UpperBound);

        Assert.True(r1.Success, $"Prima prenotazione non accettata: {r1.Error?.Message}");
        Assert.True(r2.Success, $"Seconda prenotazione adiacente accettata male: {r2.Error?.Message}");
    }

    private async Task<BookingOutcome> TryInsertBookingAsync(
        Guid spaceId,
        DateTime start,
        DateTime end,
        int seats = 1,
        SpaceAccessMode mode = SpaceAccessMode.Dedicated)
    {
        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        try
        {
            await using var ctx = _db.CreateContext();
            ctx.Bookings.Add(MakeBooking(spaceId, period, seats, mode));
            await ctx.SaveChangesAsync();
            return new BookingOutcome(true, null);
        }
        catch (Exception ex)
        {
            return new BookingOutcome(false, ex);
        }
    }

    /// <summary>
    /// Quello che fa il servizio (#102): dentro una transazione prende `pg_advisory_xact_lock`
    /// sullo spazio, conta i posti già occupati da prenotazioni `Confirmed` sovrapposte e
    /// inserisce solo se la capienza lo consente. Senza il lock due richieste simultanee
    /// leggerebbero lo stesso totale e supererebbero la capienza.
    /// </summary>
    private async Task<BookingOutcome> TryInsertMixedBookingWithLockAsync(
        Guid spaceId,
        DateTime start,
        DateTime end,
        int capacity,
        int seats)
    {
        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        try
        {
            await using var ctx = _db.CreateContext();
            await using var transaction = await ctx.Database.BeginTransactionAsync();

            await ctx.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock({spaceId})");

            var occupied = await ctx.Bookings
                .Where(b => b.SpaceId == spaceId
                            && b.Status == BookingStatus.Confirmed
                            && b.Period.Overlaps(period))
                .SumAsync(b => (int?)b.SeatsRequested) ?? 0;

            if (occupied + seats > capacity)
            {
                return new BookingOutcome(false, new InvalidOperationException($"Space {spaceId} is full"));
            }

            ctx.Bookings.Add(MakeBooking(spaceId, period, seats, SpaceAccessMode.Mixed));
            await ctx.SaveChangesAsync();
            await transaction.CommitAsync();
            return new BookingOutcome(true, null);
        }
        catch (Exception ex)
        {
            return new BookingOutcome(false, ex);
        }
    }

    private static Booking MakeBooking(Guid spaceId, NpgsqlRange<DateTime> period, int seats, SpaceAccessMode mode) =>
        new()
        {
            Id = Guid.NewGuid(),
            ReferenceCode = $"TEST-{Guid.NewGuid():N}"[..16].ToUpper(),
            SpaceId = spaceId,
            SpaceNameSnapshot = "Aula Test",
            SpaceTypeSnapshot = "Classroom",
            SpaceBuildingSnapshot = "Edificio Test",
            SpaceAccessModeSnapshot = mode,
            SpaceCapacitySnapshot = CapacityFor(mode, seats),
            OrganizerId = Guid.NewGuid(),
            OrganizerNameSnapshot = "Tester",
            Period = period,
            SeatsRequested = seats,
            IsGroup = false,
            Status = BookingStatus.Confirmed,
        };

    private static int CapacityFor(SpaceAccessMode mode, int seats) => mode == SpaceAccessMode.Dedicated ? 1 : seats * 5;

    private sealed record BookingOutcome(bool Success, Exception? Error);
}