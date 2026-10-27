using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Data.Seed;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Data;

public class BookingsSeederTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private const int SeededBookings = 13;

    private async Task InEmptyDatabaseAsync(Func<BookingsDbContext, BookingsSeeder, Task> test)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<BookingsSeeder>();

        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE outbox_emails, admin_alerts, closing_comments, equipment_requests, invitations, bookings CASCADE");

            await test(dbContext, seeder);

            await transaction.RollbackAsync();
        });
    }

    [Fact]
    public async Task The_seed_respects_every_database_constraint()
    {
        await InEmptyDatabaseAsync(async (dbContext, seeder) =>
        {
            var inserted = await seeder.InsertAsync();

            Assert.Equal(SeededBookings, inserted);
            Assert.Equal(SeededBookings, await dbContext.Bookings.CountAsync());
            Assert.Equal(SeededBookings, await dbContext.Bookings.Select(b => b.ReferenceCode).Distinct().CountAsync());
        });
    }

    [Fact]
    public async Task The_seed_covers_the_documented_edge_cases()
    {
        await InEmptyDatabaseAsync(async (dbContext, seeder) =>
        {
            await seeder.InsertAsync();

            Assert.True(await dbContext.Bookings.AnyAsync(b => b.Status == BookingStatus.Cancelled));
            Assert.True(await dbContext.Bookings.AnyAsync(b => b.Status == BookingStatus.Closed && b.ClosingComment != null));
            Assert.True(await dbContext.Bookings.AnyAsync(b => b.Status == BookingStatus.Confirmed && b.ClosingComment == null && b.Period.UpperBound < DateTime.UtcNow));
            Assert.True(await dbContext.Bookings.AnyAsync(b => b.SpaceAccessModeSnapshot == SpaceAccessMode.Mixed && b.IsGroup));
            Assert.True(await dbContext.EquipmentRequests.AnyAsync(r => r.Status == EquipmentRequestStatus.Pending));
            Assert.True(await dbContext.AdminAlerts.AnyAsync(a => a.Kind == AdminAlertKind.ReportedIssue));
            foreach (var status in Enum.GetValues<InvitationStatus>())
            {
                Assert.True(await dbContext.Invitations.AnyAsync(i => i.Status == status), $"No invitation with status {status}.");
            }
            Assert.True(await dbContext.Invitations.AnyAsync(i => i.UserId == null));
        });
    }

    [Fact]
    public async Task Two_consecutive_starts_do_not_duplicate_the_data()
    {
        await InEmptyDatabaseAsync(async (dbContext, seeder) =>
        {
            await seeder.SeedIfEmptyAsync();
            var afterFirst = await dbContext.Bookings.CountAsync();
            var invitationsAfterFirst = await dbContext.Invitations.CountAsync();

            await seeder.SeedIfEmptyAsync();

            Assert.Equal(SeededBookings, afterFirst);
            Assert.Equal(afterFirst, await dbContext.Bookings.CountAsync());
            Assert.Equal(invitationsAfterFirst, await dbContext.Invitations.CountAsync());
        });
    }

    [Fact]
    public async Task The_seed_is_skipped_when_the_database_already_has_bookings()
    {
        await InEmptyDatabaseAsync(async (dbContext, seeder) =>
        {
            dbContext.Bookings.Add(BookingTestData.NewBooking(Guid.NewGuid(), new DateTime(2035, 1, 1, 9, 0, 0, DateTimeKind.Utc), new DateTime(2035, 1, 1, 10, 0, 0, DateTimeKind.Utc)));
            await dbContext.SaveChangesAsync();

            await seeder.SeedIfEmptyAsync();

            Assert.Equal(1, await dbContext.Bookings.CountAsync());
        });
    }
}
