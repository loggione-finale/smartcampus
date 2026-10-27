using System.Net.Mail;

namespace SmartCampus.Bookings.Api.Features.Equipment;

public static class EquipmentRequestValidator
{
    public const int MaxParticipantEmails = 500;
    public const int MaxEmailLength = 254;

    public static bool TryValidateParticipantEmails(
        IReadOnlyList<string>? rawEmails,
        string? organizerEmail,
        out List<string> normalizedEmails,
        out string[] errors)
    {
        normalizedEmails = [];
        errors = [];

        if (rawEmails is null || rawEmails.Count == 0)
        {
            return true;
        }

        if (rawEmails.Count > MaxParticipantEmails)
        {
            errors = [$"Cannot invite more than {MaxParticipantEmails} participants."];
            return false;
        }

        var normalizedOrganizerEmail = string.IsNullOrWhiteSpace(organizerEmail)
            ? null
            : organizerEmail.Trim().ToLowerInvariant();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawEmails)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                errors = ["Participant email addresses cannot be empty."];
                return false;
            }

            var trimmed = raw.Trim().ToLowerInvariant();
            if (trimmed.Length > MaxEmailLength || !IsValidEmail(trimmed))
            {
                errors = [$"'{raw.Trim()}' is not a valid email address."];
                return false;
            }

            if (normalizedOrganizerEmail is not null
                && string.Equals(trimmed, normalizedOrganizerEmail, StringComparison.OrdinalIgnoreCase))
            {
                errors = ["The organizer is already included in the booking; do not include the organizer's email in participantEmails."];
                return false;
            }

            if (!seen.Add(trimmed))
            {
                errors = [$"Duplicate participant email '{trimmed}' is not allowed."];
                return false;
            }

            normalizedEmails.Add(trimmed);
        }

        return true;
    }

    public static bool TryValidateEquipmentRequests(
        IReadOnlyList<BookingEquipmentItemInput>? rawRequests,
        bool isGroupBooking,
        bool requireAtLeastOneItem,
        out List<NormalizedEquipmentItem> normalizedItems,
        out string[] errors)
    {
        normalizedItems = [];
        errors = [];

        if (rawRequests is null || rawRequests.Count == 0)
        {
            if (requireAtLeastOneItem)
            {
                errors = ["At least one equipment item is required."];
                return false;
            }

            return true;
        }

        if (!isGroupBooking)
        {
            errors = ["Only group bookings (with at least one participant) can request equipment."];
            return false;
        }

        if (rawRequests.Count > BookingEquipmentCatalog.MaxEquipmentItems)
        {
            errors = [$"Cannot request more than {BookingEquipmentCatalog.MaxEquipmentItems} equipment items."];
            return false;
        }

        var byCode = new Dictionary<string, NormalizedEquipmentItem>(StringComparer.OrdinalIgnoreCase);
        var orderedCodes = new List<string>();

        foreach (var item in rawRequests)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.EquipmentType))
            {
                errors = ["Each equipment request must specify a non-empty 'equipmentType'."];
                return false;
            }

            if (!BookingEquipmentCatalog.TryResolve(item.EquipmentType, out var catalogEntry)
                || catalogEntry is null)
            {
                errors = [$"Unknown equipment type '{item.EquipmentType.Trim()}'. Allowed values: {BookingEquipmentCatalog.AllowedCodesDescription}."];
                return false;
            }

            string? normalizedNotes = null;
            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                normalizedNotes = item.Notes.Trim();
                if (normalizedNotes.Length > BookingEquipmentCatalog.MaxNotesLength)
                {
                    errors = [$"Equipment notes cannot exceed {BookingEquipmentCatalog.MaxNotesLength} characters."];
                    return false;
                }

                if (normalizedNotes.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')))
                {
                    errors = ["Equipment notes contain invalid control characters."];
                    return false;
                }
            }

            if (catalogEntry.RequiresNotes && string.IsNullOrWhiteSpace(normalizedNotes))
            {
                errors = [$"Notes are required when requesting '{catalogEntry.Code}' ({catalogEntry.Name})."];
                return false;
            }

            if (!byCode.TryGetValue(catalogEntry.Code, out var existing))
            {
                orderedCodes.Add(catalogEntry.Code);
                byCode[catalogEntry.Code] = new NormalizedEquipmentItem(
                    CanonicalCode: catalogEntry.Code,
                    DisplayName: catalogEntry.Name,
                    Notes: normalizedNotes,
                    IsStandard: catalogEntry.IsStandard);
            }
            else if (existing.Notes is null && normalizedNotes is not null)
            {
                byCode[catalogEntry.Code] = existing with { Notes = normalizedNotes };
            }
        }

        normalizedItems = orderedCodes.Select(code => byCode[code]).ToList();
        return true;
    }

    private static bool IsValidEmail(string value)
    {
        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
        {
            return false;
        }

        var atIndex = value.IndexOf('@');
        return atIndex > 0 && value.IndexOf('.', atIndex + 1) > atIndex + 1;
    }
}
