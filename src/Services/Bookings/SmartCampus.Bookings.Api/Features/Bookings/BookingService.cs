using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class BookingService(
    BookingsDbContext dbContext,
    BookingRequestValidator validator,
    ISpacesClient spacesClient,
    IPendingClosureChecker pendingClosureChecker,
    TimeProvider timeProvider)
{
    private const string SpaceOverlapConstraint = "ex_bookings_dedicated_space_no_overlap";
    private const string PersonOverlapConstraint = "ex_invitations_person_no_overlap";
    private const string ReferenceCodeIndex = "ux_bookings_reference_code";
    private const int MaxReferenceCodeAttempts = 5;
    private const int RequestedSeats = 1;

    public async Task<CreateBookingResult> CreateAsync(
        Guid organizerId, string organizerName, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return CreateBookingResult.Invalid(errors);
        }

        var pendingReferenceCode = await pendingClosureChecker.GetPendingReferenceCodeAsync(organizerId, cancellationToken);
        if (pendingReferenceCode is not null)
        {
            return CreateBookingResult.PendingClosure(pendingReferenceCode);
        }

        var start = request.Start.UtcDateTime;
        var end = request.End.UtcDateTime;

        SpaceBookability? space;
        try
        {
            space = await spacesClient.GetBookabilityAsync(request.SpaceId, start, end, cancellationToken);
        }
        catch (SpacesUnavailableException)
        {
            return CreateBookingResult.SpacesUnavailable();
        }

        if (space is null)
        {
            return CreateBookingResult.SpaceNotFound();
        }

        if (!space.Bookable)
        {
            return CreateBookingResult.SpaceNotBookable(space.Reason);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return space.AccessMode == SpaceAccessMode.Mixed
                    ? await InsertInMixedSpaceAsync(organizerId, organizerName, request.SpaceId, space, start, end, cancellationToken)
                    : await InsertAsync(organizerId, organizerName, request.SpaceId, space, start, end, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
            {
                dbContext.ChangeTracker.Clear();

                if (postgres.ConstraintName == SpaceOverlapConstraint)
                {
                    return CreateBookingResult.SpaceConflict();
                }

                if (postgres.ConstraintName == PersonOverlapConstraint)
                {
                    return CreateBookingResult.PersonConflict();
                }

                if (postgres.ConstraintName != ReferenceCodeIndex || attempt >= MaxReferenceCodeAttempts)
                {
                    throw;
                }
            }
        }
    }

    private async Task<CreateBookingResult> InsertAsync(
        Guid organizerId, string organizerName, Guid spaceId, SpaceBookability space,
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var booking = BuildBooking(organizerId, organizerName, spaceId, space, start, end);
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateBookingResult.Created(booking);
    }

    private Task<CreateBookingResult> InsertInMixedSpaceAsync(
        Guid organizerId, string organizerName, Guid spaceId, SpaceBookability space,
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({spaceId.ToString()}, 0))", cancellationToken);

            var period = new NpgsqlRange<DateTime>(start, true, end, false);
            var occupiedSeats = await dbContext.Invitations
                .Where(i => i.Booking.SpaceId == spaceId
                    && i.Booking.Status == BookingStatus.Confirmed
                    && (i.Status == InvitationStatus.Invited || i.Status == InvitationStatus.Accepted)
                    && i.Period.Overlaps(period))
                .CountAsync(cancellationToken);

            var freeSeats = space.Capacity - occupiedSeats;
            if (freeSeats < RequestedSeats)
            {
                return CreateBookingResult.NoSeatsLeft(Math.Max(freeSeats, 0));
            }

            var booking = BuildBooking(organizerId, organizerName, spaceId, space, start, end);
            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return CreateBookingResult.Created(booking);
        });
    }

    private Booking BuildBooking(
        Guid organizerId, string organizerName, Guid spaceId, SpaceBookability space, DateTime start, DateTime end)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        var bookingId = Guid.NewGuid();

        return new Booking
        {
            Id = bookingId,
            ReferenceCode = ReferenceCodeGenerator.Generate(start),
            SpaceId = spaceId,
            SpaceNameSnapshot = space.Name,
            SpaceTypeSnapshot = space.Type,
            SpaceBuildingSnapshot = space.Building,
            SpaceAccessModeSnapshot = space.AccessMode,
            SpaceCapacitySnapshot = space.Capacity,
            OrganizerId = organizerId,
            OrganizerNameSnapshot = organizerName,
            Period = period,
            SeatsRequested = RequestedSeats,
            IsGroup = false,
            Status = BookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
            Invitations =
            [
                new Invitation
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    Email = organizerName,
                    UserId = organizerId,
                    DisplayNameSnapshot = organizerName,
                    Role = ParticipantRole.Organizer,
                    Status = InvitationStatus.Accepted,
                    Period = period,
                    ExpiresAt = start,
                    CreatedAt = now,
                    RespondedAt = now
                }
            ]
        };
    }
}
