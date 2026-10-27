namespace SmartCampus.Bookings.Api.Features.Bookings.Closure;

public interface IPendingClosureChecker
{
    Task<string?> GetPendingReferenceCodeAsync(Guid userId, CancellationToken cancellationToken);
}
