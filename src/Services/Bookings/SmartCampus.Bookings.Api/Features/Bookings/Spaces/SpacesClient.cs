using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartCampus.Bookings.Api.Features.Bookings.Spaces;

public class SpacesClient(HttpClient httpClient) : ISpacesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<SpaceBookability?> GetBookabilityAsync(
        Guid spaceId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var query = $"from={Format(from)}&to={Format(to)}";

        try
        {
            using var response = await httpClient.GetAsync($"spaces/{spaceId}/bookable?{query}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SpacesUnavailableException($"Spaces answered with status {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<SpaceBookability>(JsonOptions, cancellationToken)
                ?? throw new SpacesUnavailableException("Spaces returned an empty response.");
        }
        catch (HttpRequestException exception)
        {
            throw new SpacesUnavailableException("Spaces is not reachable.", exception);
        }
        catch (JsonException exception)
        {
            throw new SpacesUnavailableException("Spaces returned an invalid response.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpacesUnavailableException("Spaces timed out.", exception);
        }
    }

    private static string Format(DateTime value) =>
        Uri.EscapeDataString(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
