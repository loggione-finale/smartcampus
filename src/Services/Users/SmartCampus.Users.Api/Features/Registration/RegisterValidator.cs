using System.Net.Mail;
using System.Text.RegularExpressions;

namespace SmartCampus.Users.Api.Features.Registration;

public static partial class RegisterValidator
{
    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex MatricolaRegex();

    [GeneratedRegex(@"^[\p{L}\s'-]{2,50}$")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^\+39\d{9,10}$")]
    private static partial Regex PhoneNumberRegex();

    public static Dictionary<string, string[]> Validate(RegisterRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = [];
                errors[field] = list;
            }
            list.Add(message);
        }

        if (request is null)
        {
            AddError("Body", "Request body cannot be null.");
            return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
        }

        // 1. Matricola (exactly 10 digits)
        if (string.IsNullOrWhiteSpace(request.Matricola))
        {
            AddError(nameof(request.Matricola), "Matricola is required.");
        }
        else if (!MatricolaRegex().IsMatch(request.Matricola.Trim()))
        {
            AddError(nameof(request.Matricola), "Matricola must be exactly 10 digits.");
        }

        // 2. First Name (2-50 characters, letters, spaces, apostrophes, hyphens)
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            AddError(nameof(request.FirstName), "First name is required.");
        }
        else if (request.FirstName.Trim().Length < 2 || request.FirstName.Trim().Length > 50 || !NameRegex().IsMatch(request.FirstName.Trim()))
        {
            AddError(nameof(request.FirstName), "First name must be between 2 and 50 characters and contain only letters, spaces, apostrophes, or hyphens.");
        }

        // 3. Last Name (2-50 characters, letters, spaces, apostrophes, hyphens)
        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            AddError(nameof(request.LastName), "Last name is required.");
        }
        else if (request.LastName.Trim().Length < 2 || request.LastName.Trim().Length > 50 || !NameRegex().IsMatch(request.LastName.Trim()))
        {
            AddError(nameof(request.LastName), "Last name must be between 2 and 50 characters and contain only letters, spaces, apostrophes, or hyphens.");
        }

        // 4. Email (valid format, max 254 chars)
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            AddError(nameof(request.Email), "Email is required.");
        }
        else
        {
            var trimmedEmail = request.Email.Trim();
            if (trimmedEmail.Length > 254)
            {
                AddError(nameof(request.Email), "Email cannot exceed 254 characters.");
            }
            else if (!IsValidEmail(trimmedEmail))
            {
                AddError(nameof(request.Email), "Email must be a valid email address.");
            }
        }

        // 5. Phone Number (+39 followed by 9-10 digits)
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            AddError(nameof(request.PhoneNumber), "Phone number is required.");
        }
        else if (!PhoneNumberRegex().IsMatch(request.PhoneNumber.Trim()))
        {
            AddError(nameof(request.PhoneNumber), "Phone number must be in Italian format (+39 followed by 9 or 10 digits).");
        }

        // 6. Password (at least 10 characters, at least one letter and at least one digit)
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            AddError(nameof(request.Password), "Password is required.");
        }
        else
        {
            if (request.Password.Length < 10)
            {
                AddError(nameof(request.Password), "Password must be at least 10 characters long.");
            }
            if (!request.Password.Any(char.IsLetter))
            {
                AddError(nameof(request.Password), "Password must contain at least one letter.");
            }
            if (!request.Password.Any(char.IsDigit))
            {
                AddError(nameof(request.Password), "Password must contain at least one digit.");
            }
        }

        return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email && email.Contains('.') && !email.EndsWith('.');
        }
        catch
        {
            return false;
        }
    }
}
