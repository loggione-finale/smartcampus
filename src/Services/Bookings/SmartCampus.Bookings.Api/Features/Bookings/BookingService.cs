using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Api.Features.Bookings.Users;
using SmartCampus.Bookings.Api.Features.Equipment;
using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Bookings;

public class BookingService(
    BookingsDbContext dbContext,
    BookingRequestValidator validator,
    ISpacesClient spacesClient,
    IPendingClosureChecker pendingClosureChecker,
    TimeProvider timeProvider,
    IEmailOutbox emailOutbox,
    IAdminAlerts? adminAlerts = null,
    IUsersClient? usersClient = null,
    IBookingEquipmentService? bookingEquipmentService = null)
{
    private const string SpaceOverlapConstraint = "ex_bookings_dedicated_space_no_overlap";
    private const string PersonOverlapConstraint = "ex_invitations_person_no_overlap";
    private const string ReferenceCodeIndex = "ux_bookings_reference_code";
    private const string OrganizerBandIndex = "ux_bookings_organizer_date_band";
    private const int MaxReferenceCodeAttempts = 5;

    public async Task<CreateBookingResult> CreateAsync(
        Guid organizerId, string organizerName, string organizerEmail, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request, organizerEmail);
        if (errors.Count > 0)
        {
            return CreateBookingResult.Invalid(errors);
        }

        EquipmentRequestValidator.TryValidateParticipantEmails(
            request.ParticipantEmails,
            organizerEmail,
            out var normalizedParticipants,
            out _);

        EquipmentRequestValidator.TryValidateEquipmentRequests(
            request.EquipmentRequests,
            isGroupBooking: normalizedParticipants.Count > 0,
            requireAtLeastOneItem: false,
            out var normalizedEquipment,
            out _);

        var seatsRequested = 1 + normalizedParticipants.Count;

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

        if (space.AccessMode == SpaceAccessMode.Dedicated && seatsRequested > space.Capacity)
        {
            return CreateBookingResult.NoSeatsLeft(space.Capacity);
        }

        var registeredParticipants = new Dictionary<string, RegisteredUser>(StringComparer.OrdinalIgnoreCase);
        if (normalizedParticipants.Count > 0)
        {
            if (usersClient is null)
            {
                throw new InvalidOperationException("IUsersClient is required when participantEmails are provided.");
            }

            IReadOnlyList<RegisteredUser> registered;
            try
            {
                registered = await usersClient.GetRegisteredUsersAsync(normalizedParticipants, cancellationToken);
            }
            catch (UsersUnavailableException)
            {
                return CreateBookingResult.UsersUnavailable();
            }

            foreach (var user in registered)
            {
                registeredParticipants[user.Email] = user;
            }
        }

        var equipmentPlan = EquipmentResolutionPlan.Empty;
        if (normalizedEquipment.Count > 0)
        {
            if (bookingEquipmentService is null)
            {
                throw new InvalidOperationException("IBookingEquipmentService is required when equipment is requested.");
            }

            EquipmentResolutionPlan? resolvedPlan;
            try
            {
                resolvedPlan = await bookingEquipmentService.PreparePlanAsync(
                    request.SpaceId,
                    normalizedEquipment,
                    cancellationToken);
            }
            catch (SpacesUnavailableException)
            {
                return CreateBookingResult.SpacesUnavailable();
            }

            if (resolvedPlan is null)
            {
                return CreateBookingResult.SpaceNotFound();
            }

            equipmentPlan = resolvedPlan;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return space.AccessMode == SpaceAccessMode.Mixed
                    ? await InsertInMixedSpaceAsync(
                        organizerId,
                        organizerName,
                        organizerEmail,
                        request.SpaceId,
                        space,
                        start,
                        end,
                        seatsRequested,
                        normalizedParticipants,
                        registeredParticipants,
                        equipmentPlan,
                        cancellationToken)
                    : await InsertAsync(
                        organizerId,
                        organizerName,
                        organizerEmail,
                        request.SpaceId,
                        space,
                        start,
                        end,
                        seatsRequested,
                        normalizedParticipants,
                        registeredParticipants,
                        equipmentPlan,
                        cancellationToken);
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

                if (postgres.ConstraintName == OrganizerBandIndex)
                {
                    return CreateBookingResult.TimeBandAlreadyUsed();
                }

                if (postgres.ConstraintName != ReferenceCodeIndex || attempt >= MaxReferenceCodeAttempts)
                {
                    throw;
                }
            }
        }
    }

    private async Task<CreateBookingResult> InsertAsync(
        Guid organizerId,
        string organizerName,
        string organizerEmail,
        Guid spaceId,
        SpaceBookability space,
        DateTime start,
        DateTime end,
        int seatsRequested,
        IReadOnlyList<string> participantEmails,
        Dictionary<string, RegisteredUser> registeredParticipants,
        EquipmentResolutionPlan equipmentPlan,
        CancellationToken cancellationToken)
    {
        var booking = BuildBooking(
            organizerId,
            organizerName,
            organizerEmail,
            spaceId,
            space,
            start,
            end,
            seatsRequested,
            participantEmails,
            registeredParticipants,
            equipmentPlan);

        dbContext.Bookings.Add(booking);
        NotifyBookingCreated(booking);
        EnqueueInvitationEmails(booking);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RaiseNewBookingAlertAsync(booking, cancellationToken);

        if (equipmentPlan.MissingItems.Count > 0 && bookingEquipmentService is not null)
        {
            await bookingEquipmentService.RaiseMissingEquipmentAlertsAsync(booking, equipmentPlan, cancellationToken);
        }

        return CreateBookingResult.Created(booking);
    }

    private async Task RaiseNewBookingAlertAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (adminAlerts is null)
        {
            return;
        }

        await adminAlerts.RaiseAsync(
            AdminAlertKind.NewBooking,
            booking.Id,
            $"Nuova prenotazione {booking.ReferenceCode}: {booking.SpaceNameSnapshot}, {booking.OrganizerNameSnapshot}",
            cancellationToken);
    }

    private async Task<CreateBookingResult> InsertInMixedSpaceAsync(
        Guid organizerId,
        string organizerName,
        string organizerEmail,
        Guid spaceId,
        SpaceBookability space,
        DateTime start,
        DateTime end,
        int seatsRequested,
        IReadOnlyList<string> participantEmails,
        Dictionary<string, RegisteredUser> registeredParticipants,
        EquipmentResolutionPlan equipmentPlan,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        // Solo il salvataggio è nella strategia di retry: le code delle mail restano fuori,
        // così un tentativo della strategia non può accodare due volte le notifiche.
        var created = await strategy.ExecuteAsync(async () =>
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
            if (freeSeats < seatsRequested)
            {
                return null;
            }

            var created = BuildBooking(
                organizerId,
                organizerName,
                organizerEmail,
                spaceId,
                space,
                start,
                end,
                seatsRequested,
                participantEmails,
                registeredParticipants,
                equipmentPlan);

            dbContext.Bookings.Add(created);
            NotifyBookingCreated(created);
            EnqueueInvitationEmails(created);
            await dbContext.SaveChangesAsync(cancellationToken);

            await RaiseNewBookingAlertAsync(created, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return created;
        }).ConfigureAwait(false);

        if (created is null)
        {
            return CreateBookingResult.NoSeatsLeft(0);
        }

        if (equipmentPlan.MissingItems.Count > 0 && bookingEquipmentService is not null)
        {
            await bookingEquipmentService.RaiseMissingEquipmentAlertsAsync(created, equipmentPlan, cancellationToken);
        }

        return CreateBookingResult.Created(created);
    }

    private void NotifyBookingCreated(Booking booking)
    {
        emailOutbox.EnqueueBookingCreatedEmail(booking);
    }

    private void EnqueueInvitationEmails(Booking booking)
    {
        foreach (var invitation in booking.Invitations
                     .Where(i => i.Role == ParticipantRole.Participant && i.Status == InvitationStatus.Invited))
        {
            emailOutbox.EnqueueInvitationEmail(invitation, booking);
        }
    }

    private Booking BuildBooking(
        Guid organizerId,
        string organizerName,
        string organizerEmail,
        Guid spaceId,
        SpaceBookability space,
        DateTime start,
        DateTime end,
        int seatsRequested,
        IReadOnlyList<string> participantEmails,
        Dictionary<string, RegisteredUser> registeredParticipants,
        EquipmentResolutionPlan equipmentPlan)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        var bookingId = Guid.NewGuid();
        BookingTimeBands.TryResolve(start, end, out var bookingDate, out var timeBand);

        var invitations = new List<Invitation>(1 + participantEmails.Count)
        {
            new()
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                Email = organizerEmail,
                UserId = organizerId,
                DisplayNameSnapshot = organizerName,
                Role = ParticipantRole.Organizer,
                Status = InvitationStatus.Accepted,
                Period = period,
                ExpiresAt = start,
                CreatedAt = now,
                RespondedAt = now
            }
        };

        var participantExpiry = now.AddHours(2);
        if (start < participantExpiry)
        {
            participantExpiry = start;
        }

        foreach (var email in participantEmails)
        {
            registeredParticipants.TryGetValue(email, out var registeredUser);
            invitations.Add(new Invitation
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                Email = email,
                UserId = registeredUser?.Id,
                DisplayNameSnapshot = registeredUser?.DisplayName,
                Role = ParticipantRole.Participant,
                Status = InvitationStatus.Invited,
                Period = period,
                ExpiresAt = participantExpiry,
                CreatedAt = now,
                RespondedAt = null
            });
        }

        var equipmentEntities = bookingEquipmentService is not null && equipmentPlan.HasRequests
            ? bookingEquipmentService.BuildEntities(bookingId, equipmentPlan, now)
            : [];

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
            BookingDate = bookingDate,
            TimeBand = timeBand,
            SeatsRequested = seatsRequested,
            IsGroup = participantEmails.Count > 0,
            Status = BookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
            Invitations = invitations,
            EquipmentRequests = equipmentEntities
        };
    }
}
