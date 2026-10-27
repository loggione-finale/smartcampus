using System.Text;

namespace SmartCampus.Auth;

public static class SigningKeyValidator
{
    public const int MinimumKeyLengthBytes = 16;

    /// <summary>
    /// Validates that the signing key is not null/whitespace and contains at least 16 bytes (128 bits).
    /// Returns the UTF-8 encoded bytes of the key.
    /// </summary>
    public static byte[] Validate(string? signingKey, string paramName = "signingKey")
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new ArgumentException("JWT signing key cannot be null or whitespace.", paramName);
        }

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < MinimumKeyLengthBytes)
        {
            throw new ArgumentException(
                $"JWT signing key must be at least {MinimumKeyLengthBytes} bytes (128 bits) long.",
                paramName);
        }

        return keyBytes;
    }
}
