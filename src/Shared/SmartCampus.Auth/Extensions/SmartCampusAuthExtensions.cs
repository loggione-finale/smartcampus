using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SmartCampus.Auth;

public static class SmartCampusAuthExtensions
{
    /// <summary>
    /// Configures JWT Bearer authentication and the 'Admin' authorization policy
    /// using configuration settings (e.g. JWT_SECRET environment variable or Jwt:SigningKey).
    /// </summary>
    public static IServiceCollection AddSmartCampusAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SmartCampusAuthOptions>? configureOptions = null)
    {
        var signingKey = configuration[SmartCampusAuthConstants.DefaultSigningKeyEnvVar]
            ?? configuration[SmartCampusAuthConstants.DefaultSigningKeyConfigKey]
            ?? configuration[SmartCampusAuthConstants.AlternateSigningKeyConfigKey]
            ?? configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                $"JWT signing key is not configured. Set the '{SmartCampusAuthConstants.DefaultSigningKeyEnvVar}' " +
                $"environment variable or '{SmartCampusAuthConstants.DefaultSigningKeyConfigKey}' in appsettings.");
        }

        return services.AddSmartCampusAuth(signingKey, options =>
        {
            options.Issuer = configuration["Jwt:Issuer"];
            options.Audience = configuration["Jwt:Audience"];
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Configures JWT Bearer authentication and the 'Admin' authorization policy using an explicit signing key.
    /// </summary>
    public static IServiceCollection AddSmartCampusAuth(
        this IServiceCollection services,
        string signingKey,
        Action<SmartCampusAuthOptions>? configureOptions = null)
    {
        var keyBytes = SigningKeyValidator.Validate(signingKey, nameof(signingKey));

        var options = new SmartCampusAuthOptions
        {
            SigningKey = signingKey
        };
        configureOptions?.Invoke(options);

        var securityKey = new SymmetricSecurityKey(keyBytes);

        services.AddSingleton(options);

        services.AddAuthentication(authOptions =>
        {
            authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
            jwtOptions.SaveToken = true;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
                ValidIssuer = options.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                ValidAudience = options.Audience,
                ValidateLifetime = options.ValidateLifetime,
                ClockSkew = options.ClockSkew,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };
        });

        services.AddAuthorization(authzOptions =>
        {
            authzOptions.AddPolicy(SmartCampusAuthConstants.PolicyAdmin, policy =>
            {
                policy.RequireAssertion(context =>
                    context.User.IsInRole(SmartCampusAuthConstants.RoleAdmin) ||
                    context.User.HasClaim(c =>
                        (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles") &&
                        string.Equals(c.Value, SmartCampusAuthConstants.RoleAdmin, StringComparison.OrdinalIgnoreCase)));
            });
        });

        return services;
    }

    /// <summary>
    /// Adds authentication and authorization middleware to the application request pipeline.
    /// </summary>
    public static IApplicationBuilder UseSmartCampusAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
