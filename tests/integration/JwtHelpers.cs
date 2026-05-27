using Microsoft.IdentityModel.JsonWebTokens;

namespace aspirifriday.Tests;

internal static class JwtHelpers
{
    public static JsonWebToken ReadJwtToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        return handler.ReadToken(token) as JsonWebToken ?? throw new InvalidOperationException("Invalid token");
    }

    public static async Task AssertFullClaimSet(
        JsonWebToken jwt,
        Dictionary<string, string> expectedClaims)
    {
        var staticClaims = jwt.Claims
            .Where(c => c.Type is not "exp" and not "nbf" and not "iat")
            .ToDictionary(c => c.Type, c => c.Value, StringComparer.Ordinal);

        var exp = GetDateTimeFromClaim("exp");
        var nbf = GetDateTimeFromClaim("nbf");
        var iat = GetDateTimeFromClaim("iat");

        using (Assert.Multiple())
        {
            await Assert.That(staticClaims).IsEquivalentTo(expectedClaims);
            await Assert.That(exp).IsGreaterThan(DateTimeOffset.UtcNow);
            await Assert.That(nbf).IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
            await Assert.That(iat).IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
        }

        DateTimeOffset GetDateTimeFromClaim(string claimType)
        {
            var claimValue = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                throw new InvalidOperationException($"Missing required '{claimType}' claim.");
            }

            if (!long.TryParse(claimValue, out var unixSeconds))
            {
                throw new InvalidOperationException($"Claim '{claimType}' has non-numeric value '{claimValue}'.");
            }

            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
    }
}
