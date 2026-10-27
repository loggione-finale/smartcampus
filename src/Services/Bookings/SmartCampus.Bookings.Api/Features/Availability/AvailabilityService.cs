using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Availability;

public class AvailabilityService(BookingsDbContext dbContext, ISpacesClient spacesClient)
{
    public async Task<AvailabilityResult> GetAvailabilityAsync(
        Guid spaceId, DateOnly date, CancellationToken cancellationToken)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        SpaceBookability? space;
        try
        {
            space = await spacesClient.GetBookabilityAsync(spaceId, dayStart, dayEnd, cancellationToken);
        }
        catch (SpacesUnavailableException exception)
        {
            return AvailabilityResult.SpacesUnavailable(exception.Message);
        }

        if (space is null)
        {
            return AvailabilityResult.SpaceNotFound($"Space '{spaceId}' was not found.");
        }

        if (!space.Bookable)
        {
            var fullDaySlot = new AvailabilitySlotResponse(
                new DateTimeOffset(dayStart, TimeSpan.Zero),
                new DateTimeOffset(dayEnd, TimeSpan.Zero),
                OccupiedSeats: space.Capacity,
                FreeSeats: 0,
                IsAvailable: false);

            return AvailabilityResult.Success(new SpaceAvailabilityResponse(
                spaceId,
                date,
                space.AccessMode,
                space.Capacity,
                [fullDaySlot]));
        }

        var dayPeriod = new NpgsqlRange<DateTime>(dayStart, true, dayEnd, false);

        var slots = space.AccessMode == SpaceAccessMode.Mixed
            ? await GetMixedSpaceSlotsAsync(spaceId, space.Capacity, dayStart, dayEnd, dayPeriod, cancellationToken)
            : await GetDedicatedSpaceSlotsAsync(spaceId, space.Capacity, dayStart, dayEnd, dayPeriod, cancellationToken);

        return AvailabilityResult.Success(new SpaceAvailabilityResponse(
            spaceId,
            date,
            space.AccessMode,
            space.Capacity,
            slots));
    }

    private async Task<IReadOnlyList<AvailabilitySlotResponse>> GetDedicatedSpaceSlotsAsync(
        Guid spaceId, int capacity, DateTime dayStart, DateTime dayEnd, NpgsqlRange<DateTime> dayPeriod,
        CancellationToken cancellationToken)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.SpaceId == spaceId
                && b.Status == BookingStatus.Confirmed
                && b.Period.Overlaps(dayPeriod))
            .OrderBy(b => b.Period.LowerBound)
            .ToListAsync(cancellationToken);

        var slots = new List<AvailabilitySlotResponse>();
        foreach (var booking in bookings)
        {
            var start = booking.Period.LowerBound < dayStart ? dayStart : booking.Period.LowerBound;
            var end = booking.Period.UpperBound > dayEnd ? dayEnd : booking.Period.UpperBound;

            if (start < end)
            {
                slots.Add(new AvailabilitySlotResponse(
                    new DateTimeOffset(start, TimeSpan.Zero),
                    new DateTimeOffset(end, TimeSpan.Zero),
                    OccupiedSeats: capacity,
                    FreeSeats: 0,
                    IsAvailable: false));
            }
        }

        return slots;
    }

    private async Task<IReadOnlyList<AvailabilitySlotResponse>> GetMixedSpaceSlotsAsync(
        Guid spaceId, int capacity, DateTime dayStart, DateTime dayEnd, NpgsqlRange<DateTime> dayPeriod,
        CancellationToken cancellationToken)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Invitations)
            .Where(b => b.SpaceId == spaceId
                && b.Status == BookingStatus.Confirmed
                && b.Period.Overlaps(dayPeriod))
            .ToListAsync(cancellationToken);

        var segments = new List<(DateTime Start, DateTime End, int Seats)>();
        foreach (var booking in bookings)
        {
            var activeInvitations = booking.Invitations
                .Where(i => i.Status is InvitationStatus.Invited or InvitationStatus.Accepted)
                .ToList();

            if (activeInvitations.Count > 0)
            {
                foreach (var invitation in activeInvitations)
                {
                    var invStart = invitation.Period.LowerBound < dayStart ? dayStart : invitation.Period.LowerBound;
                    var invEnd = invitation.Period.UpperBound > dayEnd ? dayEnd : invitation.Period.UpperBound;
                    if (invStart < invEnd)
                    {
                        segments.Add((invStart, invEnd, 1));
                    }
                }
            }
            else
            {
                var bStart = booking.Period.LowerBound < dayStart ? dayStart : booking.Period.LowerBound;
                var bEnd = booking.Period.UpperBound > dayEnd ? dayEnd : booking.Period.UpperBound;
                if (bStart < bEnd)
                {
                    segments.Add((bStart, bEnd, booking.SeatsRequested));
                }
            }
        }

        if (segments.Count == 0)
        {
            return [];
        }

        var boundaries = segments
            .SelectMany(s => new[] { s.Start, s.End })
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var rawSlots = new List<AvailabilitySlotResponse>();
        for (var i = 0; i < boundaries.Count - 1; i++)
        {
            var subStart = boundaries[i];
            var subEnd = boundaries[i + 1];
            if (subStart >= subEnd)
            {
                continue;
            }

            var occupiedSeats = segments
                .Where(s => s.Start <= subStart && s.End >= subEnd)
                .Sum(s => s.Seats);

            if (occupiedSeats > 0)
            {
                var freeSeats = Math.Max(0, capacity - occupiedSeats);
                rawSlots.Add(new AvailabilitySlotResponse(
                    new DateTimeOffset(subStart, TimeSpan.Zero),
                    new DateTimeOffset(subEnd, TimeSpan.Zero),
                    OccupiedSeats: occupiedSeats,
                    FreeSeats: freeSeats,
                    IsAvailable: freeSeats > 0));
            }
        }

        var mergedSlots = new List<AvailabilitySlotResponse>();
        foreach (var slot in rawSlots)
        {
            if (mergedSlots.Count > 0 &&
                mergedSlots[^1].End == slot.Start &&
                mergedSlots[^1].OccupiedSeats == slot.OccupiedSeats)
            {
                var prev = mergedSlots[^1];
                mergedSlots[^1] = prev with { End = slot.End };
            }
            else
            {
                mergedSlots.Add(slot);
            }
        }

        return mergedSlots;
    }
}
