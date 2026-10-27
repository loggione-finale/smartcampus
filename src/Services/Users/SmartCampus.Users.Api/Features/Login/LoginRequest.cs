namespace SmartCampus.Users.Api.Features.Login;

public record LoginRequest(
    string? Username = null,
    string? Password = null,
    string? Identifier = null,
    string? EmailOrMatricola = null
)
{
    public string GetIdentifier()
    {
        if (!string.IsNullOrWhiteSpace(Username)) return Username.Trim();
        if (!string.IsNullOrWhiteSpace(Identifier)) return Identifier.Trim();
        if (!string.IsNullOrWhiteSpace(EmailOrMatricola)) return EmailOrMatricola.Trim();
        return string.Empty;
    }
}
