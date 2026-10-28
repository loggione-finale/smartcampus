using System.Text.Json;
using System.Text.Json.Serialization;
using SmartCampus.Bookings.Api.Domain;

namespace SmartCampus.Bookings.Api.Features.Equipment;

[JsonConverter(typeof(BookingEquipmentItemInputJsonConverter))]
public sealed record BookingEquipmentItemInput(
    string? EquipmentType,
    string? Notes = null);

public sealed class BookingEquipmentItemInputJsonConverter : JsonConverter<BookingEquipmentItemInput>
{
    public override BookingEquipmentItemInput Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new BookingEquipmentItemInput(reader.GetString(), null);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? equipmentType = null;
            string? notes = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new BookingEquipmentItemInput(equipmentType, notes);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    break;
                }

                if (string.Equals(propertyName, "equipmentType", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "code", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "type", StringComparison.OrdinalIgnoreCase))
                {
                    equipmentType = reader.TokenType == JsonTokenType.String
                        ? reader.GetString()
                        : throw new JsonException("The 'equipmentType' field must be a string.");
                }
                else if (string.Equals(propertyName, "notes", StringComparison.OrdinalIgnoreCase))
                {
                    notes = reader.TokenType switch
                    {
                        JsonTokenType.Null => null,
                        JsonTokenType.String => reader.GetString(),
                        _ => throw new JsonException("The 'notes' field must be a string or null.")
                    };
                }
                else
                {
                    reader.Skip();
                }
            }

            throw new JsonException("Incomplete equipment request object.");
        }

        throw new JsonException("Each equipment request must be either a string code or an object with 'equipmentType' and optional 'notes'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        BookingEquipmentItemInput value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.EquipmentType is not null)
        {
            writer.WriteString("equipmentType", value.EquipmentType);
        }
        else
        {
            writer.WriteNull("equipmentType");
        }

        if (value.Notes is not null)
        {
            writer.WriteString("notes", value.Notes);
        }

        writer.WriteEndObject();
    }
}

public sealed record NormalizedEquipmentItem(
    string CanonicalCode,
    string DisplayName,
    string? Notes,
    bool IsStandard);

public sealed record ResolvedEquipmentItem(
    string CanonicalCode,
    string DisplayName,
    string? Notes,
    EquipmentRequestStatus Status,
    bool MissingFromSpace);

public sealed record EquipmentResolutionPlan(
    IReadOnlyList<ResolvedEquipmentItem> Items)
{
    public static EquipmentResolutionPlan Empty { get; } = new([]);

    public bool HasRequests => Items.Count > 0;

    public IReadOnlyList<ResolvedEquipmentItem> MissingItems =>
        Items.Where(i => i.MissingFromSpace).ToList();
}

public sealed record SubmitEquipmentRequestsPayload(
    IReadOnlyList<BookingEquipmentItemInput>? EquipmentRequests);

public sealed record EquipmentRequestResponse(
    Guid Id,
    Guid BookingId,
    string EquipmentType,
    string EquipmentName,
    string? Notes,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EquipmentRequestStatus Status,
    DateTime CreatedAt)
{
    public static EquipmentRequestResponse From(EquipmentRequest entity) =>
        new(
            Id: entity.Id,
            BookingId: entity.BookingId,
            EquipmentType: entity.EquipmentType,
            EquipmentName: BookingEquipmentCatalog.GetDisplayName(entity.EquipmentType),
            Notes: entity.Notes,
            Status: entity.Status,
            CreatedAt: entity.CreatedAt);
}

public enum EquipmentOperationOutcome
{
    Succeeded,
    Invalid,
    BookingNotFound,
    Forbidden,
    BookingNotActive,
    SpaceNotFound,
    SpacesUnavailable
}

public sealed class EquipmentOperationResult
{
    public EquipmentOperationOutcome Outcome { get; private init; }
    public IReadOnlyList<EquipmentRequestResponse>? Items { get; private init; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; private init; }
    public string? Detail { get; private init; }

    public static EquipmentOperationResult Succeeded(IReadOnlyList<EquipmentRequestResponse> items) =>
        new() { Outcome = EquipmentOperationOutcome.Succeeded, Items = items };

    public static EquipmentOperationResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Outcome = EquipmentOperationOutcome.Invalid, ValidationErrors = errors };

    public static EquipmentOperationResult BookingNotFound() =>
        new() { Outcome = EquipmentOperationOutcome.BookingNotFound, Detail = "The booking does not exist." };

    public static EquipmentOperationResult Forbidden(string detail) =>
        new() { Outcome = EquipmentOperationOutcome.Forbidden, Detail = detail };

    public static EquipmentOperationResult BookingNotActive(string detail) =>
        new() { Outcome = EquipmentOperationOutcome.BookingNotActive, Detail = detail };

    public static EquipmentOperationResult SpaceNotFound() =>
        new() { Outcome = EquipmentOperationOutcome.SpaceNotFound, Detail = "The space does not exist." };

    public static EquipmentOperationResult SpacesUnavailable() =>
        new() { Outcome = EquipmentOperationOutcome.SpacesUnavailable, Detail = "The Spaces service is not reachable. Try again later." };
}
