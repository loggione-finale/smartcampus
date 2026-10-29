using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

/// <summary>
/// Background service that writes .eml files from the outbox queue.
/// Entries are inserted with status <see cref="OutboxEmailStatus.Pending"/> within the same
/// booking transaction: file writing occurs here, outside of the HTTP request,
/// so that a pickup directory issue does not fail an already saved operation.
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
                // Regular service shutdown.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error while dispatching outbox emails.");
            }
        }
    }

    /// <summary>
    /// A dispatch iteration: writes the .eml file for each Pending email and marks it Sent,
    /// or Failed with LastError if the pickup directory rejects writing.
    /// Exposed for testing, independent of the timer.
    /// </summary>
    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        // The sender uses this dispatcher's options, so test overrides
        // (or in a separate host) also apply to delivery.
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
