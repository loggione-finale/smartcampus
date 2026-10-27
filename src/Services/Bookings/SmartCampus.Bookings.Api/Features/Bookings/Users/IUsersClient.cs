namespace SmartCampus.Bookings.Api.Features.Bookings.Users;

public interface IUsersClient
{
    Task<IReadOnlyList<RegisteredUser>> GetRegisteredUsersAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default);
}
