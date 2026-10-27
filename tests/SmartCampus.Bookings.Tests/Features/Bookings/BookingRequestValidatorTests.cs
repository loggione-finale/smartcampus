using Microsoft.Extensions.Time.Testing;
using SmartCampus.Bookings.Api.Features.Bookings;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class BookingRequestValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SpaceId = Guid.NewGuid();

    private readonly BookingRequestValidator _validator = new(new FakeTimeProvider(Now));

    [Fact]
    public void Accepts_a_future_slot_aligned_to_15_minutes()
    {
        var request = new CreateBookingRequest(SpaceId, Now.AddHours(2), Now.AddHours(3));

        Assert.Empty(_validator.Validate(request));
    }

    [Fact]
    public void Rejects_an_end_before_the_start()
    {
        var request = new CreateBookingRequest(SpaceId, Now.AddHours(3), Now.AddHours(2));

        Assert.Contains(nameof(CreateBookingRequest.End), _validator.Validate(request).Keys);
    }

    [Fact]
    public void Rejects_an_end_equal_to_the_start()
    {
        var request = new CreateBookingRequest(SpaceId, Now.AddHours(2), Now.AddHours(2));

        Assert.Contains(nameof(CreateBookingRequest.End), _validator.Validate(request).Keys);
    }

    [Fact]
    public void Rejects_a_start_in_the_past()
    {
        var request = new CreateBookingRequest(SpaceId, Now.AddHours(-2), Now.AddHours(-1));

        Assert.Contains(nameof(CreateBookingRequest.Start), _validator.Validate(request).Keys);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(1)]
    public void Rejects_times_that_are_not_multiples_of_15_minutes(int minutes)
    {
        var request = new CreateBookingRequest(SpaceId, Now.AddHours(2).AddMinutes(minutes), Now.AddHours(4));

        Assert.Contains(nameof(CreateBookingRequest.Start), _validator.Validate(request).Keys);
    }

    [Fact]
    public void Rejects_times_that_are_not_in_utc()
    {
        var start = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(1));
        var request = new CreateBookingRequest(SpaceId, start, start.AddHours(1));

        Assert.Contains(nameof(CreateBookingRequest.Start), _validator.Validate(request).Keys);
    }

    [Fact]
    public void Rejects_an_empty_space()
    {
        var request = new CreateBookingRequest(Guid.Empty, Now.AddHours(2), Now.AddHours(3));

        Assert.Contains(nameof(CreateBookingRequest.SpaceId), _validator.Validate(request).Keys);
    }
}
