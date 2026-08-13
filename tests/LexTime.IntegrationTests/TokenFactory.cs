using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LexTime.Api.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace LexTime.IntegrationTests;

/// <summary>
/// Mints tokens for the boundary tests: valid, expired, and signed with the wrong key.
/// </summary>
/// <remarks>
/// The service validates tokens and never issues them, so the tests have to produce their
/// own. The reviewer-facing token — printed by the bootstrap script — arrives with feature
/// 002; until then this is the only way to exercise the accepting half of the boundary.
/// </remarks>
internal static class TokenFactory
{
    /// <summary>Issuer the test host is configured to accept.</summary>
    public const string Issuer = "lextime-tests";

    /// <summary>Audience the test host is configured to accept.</summary>
    public const string Audience = "lextime-tests";

    /// <summary>The key the test host validates against. At least 32 bytes for HMAC-SHA256.</summary>
    public const string SigningKey = "test-signing-key-not-a-secret-0123456789";

    /// <summary>A different key of the same length, used to produce a token that must be rejected.</summary>
    public const string WrongSigningKey = "other-signing-key-not-a-secret-9876543210";

    /// <summary>Creates a token that should be accepted.</summary>
    /// <returns>An encoded JWT valid for one hour.</returns>
    public static string CreateValid() =>
        Create(SigningKey, DateTime.UtcNow.AddHours(1));

    /// <summary>Creates a correctly signed token whose lifetime has already ended.</summary>
    /// <returns>An encoded JWT that expired an hour ago.</returns>
    public static string CreateExpired() =>
        Create(SigningKey, DateTime.UtcNow.AddHours(-1));

    /// <summary>Creates an unexpired token signed with a key the host does not trust.</summary>
    /// <returns>An encoded JWT with a valid shape and an invalid signature.</returns>
    public static string CreateWronglySigned() =>
        Create(WrongSigningKey, DateTime.UtcNow.AddHours(1));

    /// <summary>Builds and encodes a token.</summary>
    /// <param name="key">Signing key to use.</param>
    /// <param name="expires">Absolute expiry.</param>
    /// <returns>The encoded JWT.</returns>
    private static string Create(string key, DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            AuthenticationSetup.SigningAlgorithm);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "1")],

            // Anchored to the expiry, not to now. An expired token needs a notBefore that
            // precedes its own expiry, or the builder refuses to construct it and the test
            // fails on the fixture rather than on the boundary it is meant to exercise.
            notBefore: expires.AddHours(-2),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
