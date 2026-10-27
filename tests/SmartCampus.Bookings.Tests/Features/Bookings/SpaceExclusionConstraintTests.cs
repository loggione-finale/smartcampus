using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class SpaceExclusionConstraintTests(BookingsApiFactory factory) : IClassFixture<BookingsApiFactory>
{
    private static readonly DateTime Start = new(2031, 5, 12, 9, 0, 0, DateTimeKind.Utc);

    private async Task InsertAsync(Booking booking)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task The_database_rejects_overlapping_confirmed_bookings_on_a_dedicated_space()
    {
        var spaceId = Guid.NewGuid();
        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2)));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertAsync(BookingTestData.NewBooking(spaceId, Start.AddHours(1), Start.AddHours(3))));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("ex_bookings_dedicated_space_no_overlap", postgres.ConstraintName);
    }

    [Fact]
    public async Task The_database_accepts_adjacent_bookings_on_a_dedicated_space()
    {
        var spaceId = Guid.NewGuid();
        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2)));

        await InsertAsync(BookingTestData.NewBooking(spaceId, Start.AddHours(2), Start.AddHours(4)));
        await InsertAsync(BookingTestData.NewBooking(spaceId, Start.AddHours(-2), Start));
    }

    [Fact]
    public async Task A_cancelled_booking_does_not_block_the_slot()
    {
        var spaceId = Guid.NewGuid();
        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2), status: BookingStatus.Cancelled));

        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2)));
    }

    [Fact]
    public async Task Different_spaces_do_not_block_each_other()
    {
        await InsertAsync(BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2)));

        await InsertAsync(BookingTestData.NewBooking(Guid.NewGuid(), Start, Start.AddHours(2)));
    }

    [Fact]
    public async Task Mixed_spaces_are_not_limited_by_the_exclusion_constraint()
    {
        var spaceId = Guid.NewGuid();
        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2), SpaceAccessMode.Mixed));

        await InsertAsync(BookingTestData.NewBooking(spaceId, Start, Start.AddHours(2), SpaceAccessMode.Mixed));
    }

    [Fact]
    public async Task The_database_rejects_an_empty_period()
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertAsync(BookingTestData.NewBooking(Guid.NewGuid(), Start, Start)));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("ck_bookings_period_valid", postgres.ConstraintName);
    }

    [Fact]
    public async Task The_database_rejects_an_inverted_period()
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertAsync(BookingTestData.NewBooking(Guid.NewGuid(), Start.AddHours(2), Start)));

        Assert.IsType<PostgresException>(exception.InnerException);
    }
}
