using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace SmartCampus.Auth;

public static class JwtTokenHelper
{
    public static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// Generates a signed JWT for a given user.
    /// </summary>
    public static string GenerateToken(
        string userId,
        string email,
        string role,
        string signingKey,
        TimeSpan? lifetime = null,
        string? issuer = null,
        string? audience = null,
        string? name = null,
        IEnumerable<Claim>? additionalClaims = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be null or empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be null or empty.", nameof(role));

        name ??= email;

        var keyBytes = SigningKeyValidator.Validate(signingKey, nameof(signingKey));
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new("id", userId),
            new(JwtRegisteredClaimNames.Email, email),
            new("email", email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("role", role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (name is not null)
        {
            // Nome visualizzato, distinto dall'indirizzo email.
            claims.Add(new("name", name));
            claims.Add(new(ClaimTypes.Name, name));
        }

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        var now = DateTime.UtcNow;
        var tokenLifetime = lifetime ?? DefaultTokenLifetime;

        DateTime notBefore;
        DateTime expires;

        if (tokenLifetime < TimeSpan.Zero)
        {
            // For expired tokens in testing: issued in the past and expired in the past
            expires = now.Add(tokenLifetime);
            notBefore = expires.AddHours(-1);
        }
        else
        {
            notBefore = now;
            expires = now.Add(tokenLifetime);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            IssuedAt = notBefore,
            Expires = expires,
            SigningCredentials = credentials,
            Issuer = issuer,
            Audience = audience
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
