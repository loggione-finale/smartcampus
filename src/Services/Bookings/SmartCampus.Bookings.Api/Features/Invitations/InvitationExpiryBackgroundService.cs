using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Invitations;

/// <summary>
/// Background service that transitions unconfirmed invitations past their expiry to <see cref="InvitationStatus.Expired"/>
/// and enqueues notification emails.
/// Writes are idempotent: an already expired invitation will not be touched a second time.
/// </summary>
public class InvitationExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<InvitationExpiryOptions> options,
    ILogger<InvitationExpiryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ScanIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ExpireDueInvitationsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Regular service shutdown.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error while expiring overdue invitations.");
            }
        }
    }

    internal async Task ExpireDueInvitationsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        var dueInvitations = await dbContext.Invitations
            .Include(i => i.Booking)
            .ThenInclude(b => b.Invitations)
            .Where(i => i.Status == InvitationStatus.Invited && i.ExpiresAt <= now)
            .OrderBy(i => i.ExpiresAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var invitation in dueInvitations)
        {
            // Npgsql retry strategy does not support user-initiated transactions:
            // an explicit execution strategy is used per invitation.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            try
            {
                // Clear() at start: a new attempt will not carry over
                // tracked emails from a previous failed attempt.
                await strategy.ExecuteAsync(async cancellationToken =>
                {
                    dbContext.ChangeTracker.Clear();
                    await ExpireSingleInvitationAsync(
                            dbContext, scope, invitation, now, cancellationToken)
                        .ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception, "Failed to expire invitation {InvitationId}.", invitation.Id);
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task ExpireSingleInvitationAsync(
        BookingsDbContext dbContext,
        IServiceScope scope,
        Invitation invitation,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Refresh state within transaction: another process or
        // confirmation might have changed it in the meantime.
        var affected = await dbContext.Invitations
            .Where(i => i.Id == invitation.Id && i.Status == InvitationStatus.Invited)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(i => i.Status, InvitationStatus.Expired)
                    .SetProperty(i => i.RespondedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected > 0)
        {
            var emailOutbox = scope.ServiceProvider
                .GetRequiredService<Notifications.IEmailOutbox>();
            emailOutbox.EnqueueInvitationExpiredEmails(invitation, invitation.Booking);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
