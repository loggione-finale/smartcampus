namespace SmartCampus.Auth;

public static class SmartCampusAuthConstants
{
    public const string PolicyAdmin = "Admin";
    public const string RoleAdmin = "Admin";
    public const string RoleUser = "User";

    public const string DefaultSigningKeyEnvVar = "JWT_SECRET";
    public const string DefaultSigningKeyConfigKey = "Jwt:SigningKey";
    public const string AlternateSigningKeyConfigKey = "Jwt:Secret";
}
