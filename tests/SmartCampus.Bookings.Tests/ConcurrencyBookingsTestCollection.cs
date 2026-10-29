using Microsoft.EntityFrameworkCore;

using Npgsql;

using NpgsqlTypes;

using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Tests;

/// <summary>
/// Concurrency integration tests (issue #35): multiple simultaneous requests on the same
/// space must adhere to the non-overlapping constraint (dedicated space) and capacity
/// (mixed space); two adjacent bookings are both accepted.
/// Runs against the real `bookings_test` database (docker compose up, see infra/README.md).
/// Negative verification (test failing without constraint or lock) is a manual test:
/// procedure documented in docs/evidence/booking-concurrency.md, task #36.
/// </summary>
public sealed class BookingsTestDatabase : IAsyncLifetime
{
    // Real password comes from BOOKINGS_TEST_CONNECTION_STRING environment variable
    // (see infra/README.md); this string is the team's local development fallback.
    internal static readonly string ConnectionString = Environment.GetEnvironmentVariable("BOOKINGS_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=bookings_test;Username=bookings_app;Password=bookings";

    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        // `bookings_app` does not have rights to drop/recreate the database: it only truncates tables.
        // Table names are hardcoded (no user input), SQL injection warning does not apply.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        foreach (var table in new[] { "outbox_emails", "admin_alerts", "closing_comments", "equipment_requests", "invitations", "bookings" })
        {
#pragma warning disable EF1002 // Fixed table names, no user input
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
    /// Service behavior (#102): acquires `pg_advisory_xact_lock` on the space within a transaction,
    /// counts seats already occupied by overlapping `Confirmed` bookings, and
    /// inserts only if capacity allows. Without the lock two concurrent requests
    /// would read the same total and exceed capacity.
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