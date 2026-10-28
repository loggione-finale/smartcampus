using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCampus.Bookings.Api.Features.Bookings.Users;

public sealed class UsersClient(HttpClient httpClient) : IUsersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<RegisteredUser>> GetRegisteredUsersAsync(
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        var query = string.Join("&", emails.Select(e => $"emails={Uri.EscapeDataString(e)}"));

        try
        {
            using var response = await httpClient.GetAsync($"users?{query}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new UsersUnavailableException($"Users answered with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<List<UserDto>>(JsonOptions, cancellationToken)
                ?? throw new UsersUnavailableException("Users returned an empty response.");

            return payload
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => new RegisteredUser(u.Id, u.Email.Trim().ToLowerInvariant(), u.DisplayName ?? u.Email))
                .ToList();
        }
        catch (HttpRequestException exception)
        {
            throw new UsersUnavailableException("Users is not reachable.", exception);
        }
        catch (JsonException exception)
        {
            throw new UsersUnavailableException("Users returned an invalid response.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsersUnavailableException("Users timed out.", exception);
        }
    }

    private sealed record UserDto(Guid Id, string Email, string? DisplayName);
}
