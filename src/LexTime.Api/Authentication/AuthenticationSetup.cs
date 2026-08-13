using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace LexTime.Api.Authentication;

/// <summary>
/// Registers bearer-token validation. The service validates tokens; it never issues them
/// (FR-022) — there is no identity provider, no registration and no token endpoint.
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>Configuration section holding the token settings.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// The only signing algorithm accepted. Pinned rather than inferred, because a
    /// validator that trusts the token's own <c>alg</c> header lets an attacker choose it.
    /// </summary>
    public const string SigningAlgorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>
    /// Adds authentication and an authorisation policy that closes every endpoint by
    /// default. Anything reachable without a token has to say so explicitly, so a new
    /// endpoint is private unless someone opts it out (FR-019).
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">Configuration supplying issuer, audience and signing key.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// The issuer, audience or signing key is missing, or the key is too short for the
    /// signing algorithm. Thrown at startup rather than on the first request, so a
    /// misconfigured environment fails loudly instead of rejecting every caller.
    /// </exception>
    public static IServiceCollection AddLexTimeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var issuer = Require(section, "Issuer");
        var audience = Require(section, "Audience");
        var signingKey = Require(section, "SigningKey");

        // HMAC-SHA256 requires a key of at least 256 bits. A shorter key throws deep inside
        // the validator on first use; checking here names the problem instead.
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SigningKey must be at least 32 bytes for {SigningAlgorithm}; " +
                $"the configured key is {keyBytes.Length}.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,

                    // The default five-minute skew would let a token expired four minutes
                    // ago pass, which makes the expiry test pass for the wrong reason.
                    ClockSkew = TimeSpan.Zero,

                    ValidAlgorithms = [SigningAlgorithm],
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }

    /// <summary>Reads a required setting from the section.</summary>
    /// <param name="section">The configuration section to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The configured value.</returns>
    /// <exception cref="InvalidOperationException">The value is missing or blank.</exception>
    private static string Require(IConfigurationSection section, string key)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{key}' is required but was not set.")
            : value;
    }
}
