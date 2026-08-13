using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LexTime.Infrastructure.Maintenance;

/// <summary>
/// Mints a bearer token a reviewer can paste into the API documentation's authorize box.
/// </summary>
/// <remarks>
/// The service validates tokens and never issues them — there is no token endpoint and the
/// endpoint count in docs/prd.md §4 stays at seventeen (FR-024). This is a maintenance
/// operation, not part of the HTTP surface.
/// <para>
/// It signs with the configured key and the configured algorithm, so the printed token
/// cannot drift out of agreement with the validator. The claim set is the minimum the
/// fallback authorisation policy needs: an authenticated identity and nothing implying an
/// authorisation model that does not exist.
/// </para>
/// </remarks>
/// <param name="configuration">Supplies the issuer, audience and signing key.</param>
public sealed class DevelopmentTokenMinter(IConfiguration configuration)
{
    /// <summary>Configuration section holding the token settings.</summary>
    public const string SectionName = "Jwt";

    /// <summary>The signing algorithm. Must match what the validator accepts.</summary>
    public const string SigningAlgorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>
    /// Produces a signed token valid for the given lifetime.
    /// </summary>
    /// <param name="lifetime">
    /// How long the token remains valid. Long enough to survive an evaluation session
    /// without reissue (FR-025); the validator runs with zero clock skew, so the stated
    /// expiry is the real one.
    /// </param>
    /// <param name="issuedAtUtc">
    /// The moment the token is issued. Passed in rather than read from the clock, so this
    /// type stays testable and the seeding path's no-ambient-state rule is not breached by
    /// its neighbour.
    /// </param>
    /// <returns>The encoded JWT.</returns>
    /// <exception cref="InvalidOperationException">A required setting is missing.</exception>
    public string Mint(TimeSpan lifetime, DateTime issuedAtUtc)
    {
        var section = configuration.GetSection(SectionName);
        var issuer = Require(section, "Issuer");
        var audience = Require(section, "Audience");
        var signingKey = Require(section, "SigningKey");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SigningAlgorithm);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "development")],
            notBefore: issuedAtUtc,
            expires: issuedAtUtc.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Reads a required setting.</summary>
    /// <param name="section">The section to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The configured value.</returns>
    /// <exception cref="InvalidOperationException">The value is missing or blank.</exception>
    private static string Require(IConfigurationSection section, string key)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Configuration value '{SectionName}:{key}' is not configured.")
            : value;
    }
}
