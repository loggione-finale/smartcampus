using SmartCampus.Bookings.Api.Features.Bookings.Users;

namespace SmartCampus.Bookings.Tests.Support;

public class FakeUsersClient : IUsersClient
{
    public List<RegisteredUser> RegisteredUsers { get; set; } = [];

    public bool IsDown { get; set; }

    public Task<IReadOnlyList<RegisteredUser>> GetRegisteredUsersAsync(
        IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
    {
        if (IsDown)
        {
            throw new UsersUnavailableException("Users is not reachable.");
        }

        var found = RegisteredUsers
            .Where(u => emails.Contains(u.Email, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<RegisteredUser>>(found);
    }
}
