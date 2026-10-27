using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using SmartCampus.Bookings.Api.Data.Seed.Models;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Data.Seed;

public class BookingsSeeder(BookingsDbContext dbContext, TimeProvider timeProvider, ILogger<BookingsSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultFilePath => Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "synthetic-bookings.json");

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Bookings.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Bookings already contains data: synthetic seed skipped.");
            return;
        }

        var inserted = await InsertAsync(cancellationToken);
        logger.LogInformation("Synthetic seed loaded: {Count} bookings.", inserted);
    }

    public async Task<int> InsertAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(DefaultFilePath);
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The synthetic bookings file is empty.");

        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        var users = seed.Users.ToDictionary(u => u.Key);
        var spaces = seed.Spaces.ToDictionary(s => s.Key);

        foreach (var entry in seed.Bookings)
        {
            var booking = BuildBooking(entry, today, users, spaces);
            dbContext.Bookings.Add(booking);

            foreach (var alert in entry.Alerts ?? [])
            {
                dbContext.AdminAlerts.Add(new AdminAlert
                {
                    Id = StableId(entry.ReferenceCode, alert.Message),
                    Kind = alert.Kind,
                    BookingId = booking.Id,
                    Message = alert.Message,
                    Status = alert.Status,
                    CreatedAt = booking.CreatedAt
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return seed.Bookings.Count;
    }

    private static Booking BuildBooking(
        SeedBooking entry,
        DateTime today,
        Dictionary<string, SeedUser> users,
        Dictionary<string, SeedSpace> spaces)
    {
        var space = spaces[entry.Space];
        var organizer = users[entry.Organizer];
        var localStart = today.AddDays(entry.DaysFromToday)
            + TimeOnly.ParseExact(entry.StartTime, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();
        var start = BookingTimeBands.FromCampusTime(localStart);
        var end = start.AddMinutes(entry.DurationMinutes);
        if (!BookingTimeBands.TryResolve(start, end, out var bookingDate, out var timeBand))
        {
            throw new InvalidOperationException($"Seed booking {entry.ReferenceCode} does not fit in a single time band.");
        }

        var period = new NpgsqlRange<DateTime>(start, true, end, false);
        var createdAt = start.AddDays(-2);
        var cancelled = entry.Status == BookingStatus.Cancelled;
        var bookingId = StableId(entry.ReferenceCode, "booking");

        var booking = new Booking
        {
            Id = bookingId,
            ReferenceCode = entry.ReferenceCode,
            SpaceId = space.Id,
            SpaceNameSnapshot = space.Name,
            SpaceTypeSnapshot = space.Type,
            SpaceBuildingSnapshot = space.Building,
            SpaceAccessModeSnapshot = space.AccessMode,
            SpaceCapacitySnapshot = space.Capacity,
            OrganizerId = organizer.Id,
            OrganizerNameSnapshot = organizer.DisplayName,
            Period = period,
            BookingDate = bookingDate,
            TimeBand = timeBand,
            SeatsRequested = entry.SeatsRequested,
            IsGroup = entry.IsGroup,
            Status = entry.Status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CancelledAt = cancelled ? start.AddDays(-1) : null,
            CancelledBy = cancelled ? organizer.Id : null,
            CancellationReason = cancelled ? entry.CancellationReason : null
        };

        booking.Invitations.Add(new Invitation
        {
            Id = StableId(entry.ReferenceCode, "organizer"),
            BookingId = bookingId,
            Email = organizer.Email,
            UserId = organizer.Id,
            DisplayNameSnapshot = organizer.DisplayName,
            Role = ParticipantRole.Organizer,
            Status = cancelled ? InvitationStatus.Cancelled : InvitationStatus.Accepted,
            Period = period,
            ExpiresAt = createdAt.AddHours(2),
            CreatedAt = createdAt,
            RespondedAt = createdAt
        });

        foreach (var invitation in entry.Invitations ?? [])
        {
            var user = invitation.User is null ? null : users[invitation.User];
            var email = user?.Email ?? invitation.Email
                ?? throw new InvalidOperationException($"Invitation without user or email in {entry.ReferenceCode}.");

            booking.Invitations.Add(new Invitation
            {
                Id = StableId(entry.ReferenceCode, email),
                BookingId = bookingId,
                Email = email,
                UserId = user?.Id,
                DisplayNameSnapshot = user?.DisplayName,
                Role = invitation.Role,
                Status = invitation.Status,
                Period = period,
                ExpiresAt = createdAt.AddHours(2),
                CreatedAt = createdAt,
                RespondedAt = invitation.Status == InvitationStatus.Invited ? null : createdAt.AddHours(1)
            });
        }

        foreach (var request in entry.EquipmentRequests ?? [])
        {
            booking.EquipmentRequests.Add(new EquipmentRequest
            {
                Id = StableId(entry.ReferenceCode, request.EquipmentType),
                BookingId = bookingId,
                EquipmentType = request.EquipmentType,
                Notes = request.Notes,
                Status = request.Status,
                CreatedAt = createdAt
            });
        }

        if (entry.ClosingComment is { } closing)
        {
            booking.ClosingComment = new ClosingComment
            {
                Id = StableId(entry.ReferenceCode, "closing"),
                BookingId = bookingId,
                AuthorId = organizer.Id,
                SpaceUsed = closing.SpaceUsed,
                AttendeesCount = closing.AttendeesCount,
                IssuesReported = closing.IssuesReported,
                Comment = closing.Comment,
                CreatedAt = end.AddHours(1)
            };
        }

        return booking;
    }

    private static Guid StableId(string referenceCode, string part)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{referenceCode}:{part}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
