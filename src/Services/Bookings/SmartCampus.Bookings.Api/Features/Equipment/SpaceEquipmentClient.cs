using System.Net;
using System.Text.Json;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;

namespace SmartCampus.Bookings.Api.Features.Equipment;

public sealed class SpaceEquipmentClient(HttpClient httpClient) : ISpaceEquipmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlySet<string>?> GetSpaceEquipmentCodesAsync(
        Guid spaceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"spaces/{spaceId:D}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SpacesUnavailableException($"Spaces answered with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<SpaceEquipmentDetailDto>(JsonOptions, cancellationToken)
                ?? throw new SpacesUnavailableException("Spaces returned an empty space detail response.");

            var normalizedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (payload.Equipment is not null)
            {
                foreach (var raw in payload.Equipment)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    if (BookingEquipmentCatalog.TryResolve(raw, out var entry) && entry is not null)
                    {
                        normalizedSet.Add(entry.Code);
                    }
                    else
                    {
                        normalizedSet.Add(raw.Trim());
                    }
                }
            }

            return normalizedSet;
        }
        catch (HttpRequestException ex)
        {
            throw new SpacesUnavailableException("Spaces is not reachable.", ex);
        }
        catch (JsonException ex)
        {
            throw new SpacesUnavailableException("Spaces returned an invalid space detail response.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpacesUnavailableException("Spaces timed out.", ex);
        }
    }

    private sealed record SpaceEquipmentDetailDto(IReadOnlyList<string>? Equipment);
}
