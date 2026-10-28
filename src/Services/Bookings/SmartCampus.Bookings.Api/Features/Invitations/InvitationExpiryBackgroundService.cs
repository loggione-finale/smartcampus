using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

using SmartCampus.Bookings.Api.Features.Notifications;

namespace SmartCampus.Bookings.Api.Features.Invitations;

/// <summary>
/// Servizio in background che porta a <see cref="InvitationStatus.Expired"/> gli inviti
/// non confermati oltre la scadenza e mette in coda le mail di notifica.
/// La scrittura è idempotente: un invito già scaduto non viene toccato una seconda volta.
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
                // Arresto regolare del servizio.
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
            .Where(i => i.Status == InvitationStatus.Invited && i.ExpiresAt <= now)
            .OrderBy(i => i.ExpiresAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var invitation in dueInvitations)
        {
            // La strategia di retry di Npgsql non supporta transazioni utente:
            // per-invito si usa una strategia di esecuzione esplicita.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync<object?>(
                    cancellationToken => ExpireSingleInvitationAsync(
                            dbContext, scope, invitation, now, cancellationToken)
                        .ContinueWith<object?>(_ => null, CancellationToken.None),
                    cancellationToken)
                    .ConfigureAwait(false);
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

        // Riaggiorna lo stato dentro la transazione: un altro processo o
        // una conferma potrebbero averlo cambiato nel frattempo.
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
            await emailOutbox.EnqueueInvitationExpiredEmailAsync(
                invitation, invitation.Booking, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
