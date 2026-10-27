namespace SmartCampus.Auth;

public class SmartCampusAuthOptions
{
    public string? SigningKey { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = false;
    public bool ValidateLifetime { get; set; } = true;
    public TimeSpan ClockSkew { get; set; } = TimeSpan.Zero;
}
