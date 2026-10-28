using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Notifications;

public interface INotificationSender
{
    // Does not save: the caller saves, so the email is written together with its own changes.
    void QueueEmail(string toAddress, string subject, string body, string kind, Guid? bookingId);
}

public sealed class NotificationSender(BookingsDbContext dbContext, TimeProvider timeProvider) : INotificationSender
{
    public void QueueEmail(string toAddress, string subject, string body, string kind, Guid? bookingId)
    {
        dbContext.OutboxEmails.Add(new OutboxEmail
        {
            Id = Guid.NewGuid(),
            ToAddress = toAddress,
            Subject = subject,
            Body = body.Length > 8000 ? body[..8000] : body,
            Kind = kind,
            BookingId = bookingId,
            Status = OutboxEmailStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });
    }
}
