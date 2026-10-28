using Microsoft.EntityFrameworkCore;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Equipment;

public sealed class BookingEquipmentService(
    BookingsDbContext dbContext,
    ISpaceEquipmentClient spaceEquipmentClient,
    IAdminAlerts adminAlerts,
    TimeProvider timeProvider) : IBookingEquipmentService
{
    public async Task<EquipmentResolutionPlan?> PreparePlanAsync(
        Guid spaceId,
        IReadOnlyList<NormalizedEquipmentItem> requestedItems,
        CancellationToken cancellationToken = default)
    {
        if (requestedItems.Count == 0)
        {
            return EquipmentResolutionPlan.Empty;
        }

        var availableCodes = await spaceEquipmentClient.GetSpaceEquipmentCodesAsync(spaceId, cancellationToken);
        if (availableCodes is null)
        {
            return null;
        }

        var resolved = new List<ResolvedEquipmentItem>(requestedItems.Count);
        foreach (var item in requestedItems)
        {
            var presentInSpace = item.IsStandard && availableCodes.Contains(item.CanonicalCode);
            resolved.Add(new ResolvedEquipmentItem(
                CanonicalCode: item.CanonicalCode,
                DisplayName: item.DisplayName,
                Notes: item.Notes,
                Status: presentInSpace ? EquipmentRequestStatus.Confirmed : EquipmentRequestStatus.Pending,
                MissingFromSpace: !presentInSpace));
        }

        return new EquipmentResolutionPlan(resolved);
    }

    public List<EquipmentRequest> BuildEntities(
        Guid bookingId,
        EquipmentResolutionPlan plan,
        DateTime createdAtUtc)
    {
        if (!plan.HasRequests)
        {
            return [];
        }

        return plan.Items.Select(item => new EquipmentRequest
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            EquipmentType = item.CanonicalCode,
            Notes = item.Notes,
            Status = item.Status,
            CreatedAt = createdAtUtc
        }).ToList();
    }

    public async Task RaiseMissingEquipmentAlertsAsync(
        Booking booking,
        EquipmentResolutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        foreach (var missing in plan.MissingItems)
        {
            var notesSuffix = string.IsNullOrWhiteSpace(missing.Notes)
                ? string.Empty
                : $" ({missing.Notes})";

            var message =
                $"Richiesta di strumento mancante nell'aula {booking.SpaceNameSnapshot} per {booking.ReferenceCode}: {missing.DisplayName}{notesSuffix}";

            if (message.Length > 1000)
            {
                message = message[..1000];
            }

            await adminAlerts.RaiseAsync(
                AdminAlertKind.EquipmentRequest,
                booking.Id,
                message,
                cancellationToken);
        }
    }

    public async Task<EquipmentOperationResult> GetByBookingIdAsync(
        Guid bookingId,
        Guid callerUserId,
        string? callerEmail,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Invitations)
            .Include(b => b.EquipmentRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return EquipmentOperationResult.BookingNotFound();
        }

        if (!isAdmin && !IsParticipantOrOrganizer(booking, callerUserId, callerEmail))
        {
            return EquipmentOperationResult.Forbidden("Only booking participants or admins can view equipment requests.");
        }

        var responses = booking.EquipmentRequests
            .OrderBy(r => r.CreatedAt)
            .Select(EquipmentRequestResponse.From)
            .ToList();

        return EquipmentOperationResult.Succeeded(responses);
    }

    public async Task<EquipmentOperationResult> AddToBookingAsync(
        Guid bookingId,
        Guid callerUserId,
        SubmitEquipmentRequestsPayload payload,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(b => b.EquipmentRequests)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return EquipmentOperationResult.BookingNotFound();
        }

        if (booking.OrganizerId != callerUserId)
        {
            return EquipmentOperationResult.Forbidden("Only the booking organizer can request equipment.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (booking.Status != BookingStatus.Confirmed || booking.Period.UpperBound <= now)
        {
            return EquipmentOperationResult.BookingNotActive("Equipment can only be requested for active confirmed bookings.");
        }

        if (!EquipmentRequestValidator.TryValidateEquipmentRequests(
                payload.EquipmentRequests,
                isGroupBooking: booking.IsGroup,
                requireAtLeastOneItem: true,
                out var normalizedItems,
                out var validationErrors))
        {
            return EquipmentOperationResult.Invalid(new Dictionary<string, string[]>
            {
                ["EquipmentRequests"] = validationErrors
            });
        }

        var existingTypes = booking.EquipmentRequests
            .Select(r => r.EquipmentType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newItems = normalizedItems
            .Where(i => !existingTypes.Contains(i.CanonicalCode))
            .ToList();

        if (newItems.Count == 0)
        {
            var currentResponses = booking.EquipmentRequests
                .OrderBy(r => r.CreatedAt)
                .Select(EquipmentRequestResponse.From)
                .ToList();
            return EquipmentOperationResult.Succeeded(currentResponses);
        }

        EquipmentResolutionPlan? plan;
        try
        {
            plan = await PreparePlanAsync(booking.SpaceId, newItems, cancellationToken);
        }
        catch (SpacesUnavailableException)
        {
            return EquipmentOperationResult.SpacesUnavailable();
        }

        if (plan is null)
        {
            return EquipmentOperationResult.SpaceNotFound();
        }

        var entities = BuildEntities(booking.Id, plan, now);
        dbContext.EquipmentRequests.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RaiseMissingEquipmentAlertsAsync(booking, plan, cancellationToken);

        var allResponses = booking.EquipmentRequests
            .OrderBy(r => r.CreatedAt)
            .Select(EquipmentRequestResponse.From)
            .ToList();

        return EquipmentOperationResult.Succeeded(allResponses);
    }

    private static bool IsParticipantOrOrganizer(Booking booking, Guid callerUserId, string? callerEmail)
    {
        if (booking.OrganizerId == callerUserId)
        {
            return true;
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(callerEmail)
            ? null
            : callerEmail.Trim();

        return booking.Invitations.Any(i =>
            (i.UserId.HasValue && i.UserId.Value == callerUserId)
            || (normalizedEmail is not null && string.Equals(i.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)));
    }
}
