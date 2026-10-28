using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Servizio in background che scrive i file .eml della coda di outbox.
/// Le code sono inserite con stato <see cref="OutboxEmailStatus.Pending"/> nella stessa
/// transazione della prenotazione: la scrittura del file avviene qui, fuori dalla richiesta
/// HTTP, così un problema della pickup directory non fa fallire un'operazione già salvata.
/// </summary>
public class EmailOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    ILogger<EmailOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.DispatchIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await DispatchPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Arresto regolare del servizio.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error while dispatching outbox emails.");
            }
        }
    }

    /// <summary>
    /// Un colpo di dispatch: scrive il .eml di ogni mail in stato Pending e la marca Sent,
    /// oppure Failed con LastError se la pickup directory rifiuta la scrittura.
    /// Esposto per i test, indipendente dal timer.
    /// </summary>
    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        // Il sender usa le opzioni di questo dispatcher, così un override in test
        // (o in un host separato) si applica anche alla consegna.
        var sender = new PickupDirectoryEmailSender(options);

        var pending = await dbContext.OutboxEmails
            .Where(e => e.Status == OutboxEmailStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var email in pending)
        {
            try
            {
                await sender.SendAsync(
                    new OutgoingEmail(
                        email.ToAddress, email.Subject, email.Body, email.Kind, email.BookingId),
                    cancellationToken)
                    .ConfigureAwait(false);

                email.Status = OutboxEmailStatus.Sent;
                email.SentAt = DateTime.UtcNow;
                email.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                email.Status = OutboxEmailStatus.Failed;
                email.Attempts++;
                email.LastError = exception.Message;
                logger.LogError(
                    exception, "Failed to write outbox email {OutboxEmailId} to the pickup directory.",
                    email.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
